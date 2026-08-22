import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';

const runCoaching = process.env.E2E_RUN_COACHING === 'true';

async function login(request, email, password) {
  const response = await request.post('/api/auth/login', {
    data: { email, password }
  });

  expect(response.status(), `login failed for ${email}`).toBe(200);
  const body = await response.json();
  if (body.requiresMfa) {
    throw new Error('Disposable coaching fixtures must not require interactive MFA.');
  }

  expect(body.accessToken, `login did not return an access token for ${email}`).toEqual(expect.any(String));
  return body;
}

async function getProfile(request, accessToken) {
  const response = await request.get('/api/users/me', {
    headers: { Authorization: `Bearer ${accessToken}` }
  });

  expect(response.status()).toBe(200);
  const profile = await response.json();
  expect(profile.userId).toEqual(expect.any(String));
  return profile;
}

function createNotificationHub(baseUrl, accessToken) {
  const hubUrl = new URL('/hubs/notifications', baseUrl);
  hubUrl.protocol = hubUrl.protocol === 'https:' ? 'wss:' : 'ws:';
  hubUrl.searchParams.set('access_token', accessToken);

  const socket = new WebSocket(hubUrl);
  const notifications = [];
  const waiters = [];
  let handshakeCompleted = false;
  let closed = false;

  const failWaiters = (error) => {
    while (waiters.length > 0) {
      waiters.shift().reject(error);
    }
  };

  const parseMessages = (rawData) => {
    const text = typeof rawData === 'string'
      ? rawData
      : Buffer.from(rawData).toString('utf8');

    for (const frame of text.split('\x1e')) {
      if (!frame) continue;

      const message = JSON.parse(frame);
      if (!handshakeCompleted) {
        if (message.error) {
          throw new Error(`SignalR handshake failed: ${message.error}`);
        }
        handshakeCompleted = true;
        continue;
      }

      if (message.type !== 1 || message.target !== 'ReceiveNotification') continue;

      const notification = message.arguments?.[0];
      if (!notification) continue;
      notifications.push(notification);

      while (waiters.length > 0) {
        waiters.shift().resolve(notification);
      }
    }
  };

  const ready = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('SignalR connection timed out.')), 15_000);

    socket.addEventListener('open', () => {
      socket.send('{"protocol":"json","version":1}\x1e');
    });

    socket.addEventListener('message', (event) => {
      try {
        parseMessages(event.data);
        if (handshakeCompleted) {
          clearTimeout(timeout);
          resolve();
        }
      } catch (error) {
        clearTimeout(timeout);
        reject(error);
      }
    });

    socket.addEventListener('error', () => {
      clearTimeout(timeout);
      reject(new Error('SignalR WebSocket connection failed.'));
    });

    socket.addEventListener('close', (event) => {
      closed = true;
      if (!handshakeCompleted) {
        clearTimeout(timeout);
        reject(new Error(`SignalR connection closed before handshake (${event.code}).`));
      }
      failWaiters(new Error(`SignalR connection closed (${event.code}).`));
    });
  });

  return {
    async waitForNotification(timeoutMs = 20_000) {
      const existing = notifications.at(-1);
      if (existing) return existing;

      return new Promise((resolve, reject) => {
        const waiter = {
          resolve: notification => {
            clearTimeout(timeout);
            resolve(notification);
          },
          reject: error => {
            clearTimeout(timeout);
            reject(error);
          }
        };
        const timeout = setTimeout(() => {
          const index = waiters.indexOf(waiter);
          if (index >= 0) waiters.splice(index, 1);
          reject(new Error('SignalR notification was not received before the deadline.'));
        }, timeoutMs);

        waiters.push(waiter);
      });
    },
    async close() {
      if (closed) return;
      socket.close(1000, 'E2E complete');
      await new Promise(resolve => socket.addEventListener('close', resolve, { once: true }));
    },
    ready
  };
}

async function expectAnonymousHubRejected(baseUrl) {
  const hubUrl = new URL('/hubs/notifications', baseUrl);
  hubUrl.protocol = hubUrl.protocol === 'https:' ? 'wss:' : 'ws:';

  await new Promise((resolve, reject) => {
    const socket = new WebSocket(hubUrl);
    let settled = false;
    let opened = false;
    const timeout = setTimeout(() => {
      if (settled) return;
      settled = true;
      socket.close();
      reject(new Error('Anonymous SignalR connection was not rejected before the deadline.'));
    }, 10_000);

    const rejectIfOpen = () => {
      if (settled) return;
      settled = true;
      clearTimeout(timeout);
      socket.close();
      reject(new Error('Anonymous SignalR connection unexpectedly opened.'));
    };

    socket.addEventListener('open', () => {
      opened = true;
      rejectIfOpen();
    });
    socket.addEventListener('error', () => {
      if (settled || opened) return;
      settled = true;
      clearTimeout(timeout);
      resolve();
    });
    socket.addEventListener('close', (event) => {
      if (settled) return;
      settled = true;
      clearTimeout(timeout);
      if (opened) {
        reject(new Error(`Anonymous SignalR connection opened then closed (${event.code}).`));
      } else {
        resolve();
      }
    });
  });
}

test.describe('Disposable coaching write/read and SignalR flow', () => {
  test.skip(
    !runCoaching,
    'Set E2E_RUN_COACHING=true only for a disposable coaching tenant.'
  );

  test('rejects an anonymous SignalR connection', async ({ baseURL }) => {
    await expectAnonymousHubRejected(baseURL);
  });

  test('teacher creates an assignment, student reads it, and SignalR receives the notification', async ({ request, baseURL }) => {
    test.setTimeout(60_000);

    const teacher = await login(
      request,
      process.env.E2E_COACHING_TEACHER_EMAIL,
      process.env.E2E_COACHING_TEACHER_PASSWORD);
    const student = await login(
      request,
      process.env.E2E_COACHING_STUDENT_EMAIL,
      process.env.E2E_COACHING_STUDENT_PASSWORD);

    const teacherProfile = await getProfile(request, teacher.accessToken);
    const studentProfile = await getProfile(request, student.accessToken);
    const teacherId = teacherProfile.userId;
    const studentId = studentProfile.userId;
    const institutionId = teacherProfile.teacherDetails?.institutionId;

    expect(institutionId).toEqual(expect.any(String));
    expect(studentProfile.studentDetails?.institutionId).toBe(institutionId);

    const hub = createNotificationHub(baseURL, student.accessToken);
    await hub.ready;

    const nonce = randomUUID();
    const idempotencyKey = `e2e-coaching-${nonce}`;
    const assignment = {
      teacherId,
      institutionId,
      title: `E2E coaching assignment ${nonce}`,
      description: 'Disposable E2E assignment; it must be deleted by the test.',
      subject: 'Matematik',
      assignmentType: 'Individual',
      assignmentSource: 'Digital',
      dueDate: new Date(Date.now() + 86_400_000).toISOString(),
      estimatedDurationMinutes: 30,
      studentIds: [studentId]
    };

    let assignmentId;
    try {
      const createResponse = await request.post('/api/assignments', {
        data: assignment,
        headers: {
          Authorization: `Bearer ${teacher.accessToken}`,
          'Idempotency-Key': idempotencyKey
        }
      });
      expect(createResponse.status()).toBe(201);
      const created = await createResponse.json();
      assignmentId = created.assignmentId;
      expect(assignmentId).toEqual(expect.any(String));

      const replayResponse = await request.post('/api/assignments', {
        data: assignment,
        headers: {
          Authorization: `Bearer ${teacher.accessToken}`,
          'Idempotency-Key': idempotencyKey
        }
      });
      expect(replayResponse.status()).toBe(201);
      expect((await replayResponse.json()).assignmentId).toBe(assignmentId);

      const listResponse = await request.get(`/api/assignments/student/${studentId}?pageNumber=1&pageSize=25`, {
        headers: { Authorization: `Bearer ${student.accessToken}` }
      });
      expect(listResponse.status()).toBe(200);
      const list = await listResponse.json();
      expect(list.items.some(item => item.id === assignmentId)).toBeTruthy();

      const detailResponse = await request.get(`/api/assignments/${assignmentId}`, {
        headers: { Authorization: `Bearer ${student.accessToken}` }
      });
      expect(detailResponse.status()).toBe(200);
      const detail = await detailResponse.json();
      expect(detail.id).toBe(assignmentId);
      expect(detail.assignedStudents).toHaveLength(1);
      expect(detail.assignedStudents[0].studentId).toBe(studentId);

      const notification = await hub.waitForNotification();
      expect(notification.type).toBe('AssignmentCreated');
      expect(notification.relatedEntityId).toBe(assignmentId);
    } finally {
      if (assignmentId) {
        const deleteResponse = await request.delete(`/api/assignments/${assignmentId}`, {
          headers: { Authorization: `Bearer ${teacher.accessToken}` }
        });
        expect([204, 404]).toContain(deleteResponse.status());
      }
      await hub.close();
    }
  });
});
