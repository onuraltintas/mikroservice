/**
 * Motion Path Engine
 * Eye Tracking, Fixation ve Saccade egzersizleri için.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, boundedText, recordOrEmpty } from './reading-pacer-safety';

function field(record: Record<string, any>, name: string): any {
    const key = Object.keys(record).reverse().find(candidate => candidate.toLowerCase() === name.toLowerCase());
    return key === undefined ? undefined : record[key];
}

export interface MotionPathConfig extends EngineConfig {
    path: {
        type: 'horizontal' | 'vertical' | 'circle' | 'infinity8' | 'random_point' | 'two_point_jump';
        lines?: number;
        direction?: string;
    };
    target: {
        type: 'dot' | 'circle' | 'arrow';
        size: 'small' | 'medium' | 'large';
        color?: string;
    };
    movement: {
        speedLevel: number;
        durationSec?: number;
        jumpIntervalMs?: number;
        fixationTimeMs?: number;
    };
    fixation?: {
        points: number;
        peripheralCount: number;
        pointSize?: number;
    };
}

interface PeripheralChar {
    char: string;
    position: 'top' | 'bottom' | 'left' | 'right';
    x: number;
    y: number;
}

export class MotionPathEngine implements BaseEngine {
    readonly engineType = 'motion_path';
    readonly displayName = 'Göz Egzersizi';

    state: EngineState = {
        isRunning: false,
        isPaused: false,
        isCompleted: false,
        currentStep: 0,
        totalSteps: 0,
        score: 0,
        accuracy: 100,
        timeElapsed: 0,
        errors: 0,
        currentValue: ''
    };

    private config!: MotionPathConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private animationFrame: any;
    private jumpInterval: any;
    private transitionTimeout: any;
    private feedbackTimeout: any;
    private saccadeUnlockTimeout: any;

    private targetX = 50;
    private targetY = 50;
    private angle = 0;
    private direction = 1;
    private isJumping = false;

    private fixationProgress = 0;
    private fixationStartTime = 0;
    private peripheralChars: PeripheralChar[] = [];
    private currentPointSize = 36;
    private fixationResults: Array<{
        pointIndex: number;
        peripheralChars: PeripheralChar[];
        userInput: string;
        correctChars: string;
        accuracy: number;
        fixationTimeMs: number;
        timestamp: string;
    }> = [];

    // Peripheral Vision Testing State
    private awaitingPeripheralInput = false;
    private currentCorrectChars = '';
    private peripheralInputBuffer = '';
    private totalPeripheralTests = 0;
    private correctPeripheralTests = 0;

    // Audio/Metronome Support
    private audioContext: AudioContext | null = null;
    private useMetronome = false;

    // Saccade State
    private saccadeTargets: any[] = [];
    private currentTargetIndex = 0;
    private saccadeInputLocked = false;

    // Current mode (fixation, saccade, tracking, etc.)
    private currentMode = 'fixation';

    // Time-based mode
    private isTimeBased = false;
    private durationSeconds = 0;

    // Feedback State
    private lastFeedback: { isCorrect: boolean; userInput: string; correctChars: string } | null = null;
    private showingFeedback = false;
    private fixationPhase: 'idle' | 'transition' | 'holding' | 'awaitingInput' | 'feedback' = 'idle';
    private phaseStartedAt = 0;
    private phaseDuration = 0;
    private phaseRemaining = 0;
    private pendingTargetX = 50;
    private pendingTargetY = 50;

    // Track last used chars to avoid repetition
    private lastUsedChars: string[] = [];
    private configuredTotalSteps = 0;

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;
        const backendConfig = config as any;
        const nestedConfig = recordOrEmpty(field(backendConfig, 'engineConfig'));
        const engineConfig = { ...backendConfig, ...nestedConfig };
        const mergedContainer = (name: string) => ({
            ...recordOrEmpty(field(backendConfig, name)),
            ...recordOrEmpty(field(nestedConfig, name))
        });
        const timing = mergedContainer('timing');
        const content = mergedContainer('content');
        const fixation = mergedContainer('fixation');
        const movement = mergedContainer('movement');
        const path = mergedContainer('path');
        const target = mergedContainer('target');
        const pathType = typeof field(path, 'type') === 'string' ? String(field(path, 'type')).toLowerCase() : '';
        const targetType = typeof field(target, 'type') === 'string' ? String(field(target, 'type')).toLowerCase() : '';
        const targetSize = typeof field(target, 'size') === 'string' ? String(field(target, 'size')).toLowerCase() : '';
        const configuredMode = field(nestedConfig, 'mode') ?? field(backendConfig, 'mode');
        const rawMode = typeof configuredMode === 'string' ? configuredMode.toLowerCase() : '';
        const mode = ['fixation', 'saccade', 'tracking'].includes(rawMode)
            ? rawMode : 'fixation';
        const durationMsValue = field(timing, 'durationMs');
        const durationMs = typeof durationMsValue === 'number' && durationMsValue > 0
            ? boundedInteger(durationMsValue, 60_000, 5_000, 3_600_000) : 0;
        const durationSecondsValue = field(timing, 'totalDurationSeconds') ?? field(timing, 'durationSeconds');
        const durationSeconds = durationMs > 0
            ? Math.round(durationMs / 1000)
            : (typeof durationSecondsValue === 'number' && durationSecondsValue > 0
                ? boundedInteger(durationSecondsValue, 60, 5, 3600) : 0);
        const holdMs = boundedInteger(
            field(timing, 'holdMs') ?? field(movement, 'fixationTimeMs'),
            mode === 'fixation' ? 2000 : 1000, 50, 10000);

        this.currentMode = mode;
        this.isTimeBased = durationSeconds > 0;
        this.durationSeconds = durationSeconds;
        this.config = {
            ...engineConfig,
            path: {
                ...path,
                type: ['horizontal', 'vertical', 'circle', 'infinity8', 'random_point', 'two_point_jump'].includes(pathType)
                    ? pathType : 'horizontal'
            },
            target: {
                ...target,
                type: ['dot', 'circle', 'arrow'].includes(targetType) ? targetType : 'dot',
                size: ['small', 'medium', 'large'].includes(targetSize) ? targetSize : 'medium',
                color: field(target, 'color')
            },
            movement: {
                ...movement,
                speedLevel: boundedInteger(field(movement, 'speedLevel'), 1, 1, 5),
                jumpIntervalMs: boundedInteger(field(movement, 'jumpIntervalMs'), 1000, 50, 10000),
                fixationTimeMs: holdMs
            }
        } as MotionPathConfig;

        if (mode === 'saccade') {
            const configuredTargets = field(nestedConfig, 'targets') ?? field(backendConfig, 'targets');
            const rawTargets = Array.isArray(configuredTargets) ? configuredTargets.slice(0, 500) : [];
            this.saccadeTargets = rawTargets
                .filter((item: unknown) => item !== null && typeof item === 'object' && !Array.isArray(item))
                .map((item: any) => ({
                    x: boundedInteger(field(item, 'x'), 50, 0, 100),
                    y: boundedInteger(field(item, 'y'), 50, 0, 100),
                    size: boundedInteger(field(item, 'size'), 30, 8, 200),
                    value: boundedText(field(item, 'value'), '', 100),
                    number: this.normalizeTargetNumber(field(item, 'number'))
                }));
            if (this.saccadeTargets.length === 0)
                this.generateSaccadeTargets(content);
            this.state.totalSteps = this.isTimeBased ? 0 : this.saccadeTargets.length;
        } else {
            const peripheralCount = boundedInteger(field(content, 'peripheralCount') ?? field(fixation, 'peripheralCount'), 0, 0, 4);
            const pointSize = boundedInteger(field(content, 'pointSize') ?? field(fixation, 'pointSize'), 36, 8, 200);
            const configuredPoints = boundedInteger(field(content, 'points') ?? field(fixation, 'points'), 10, 1, 500);
            const points = this.isTimeBased
                ? Math.min(500, Math.max(1, Math.floor((durationSeconds * 1000) / holdMs)))
                : configuredPoints;
            this.config.fixation = { points, peripheralCount, pointSize };
            this.currentPointSize = pointSize;
            this.state.totalSteps = points;
        }
        this.configuredTotalSteps = this.state.totalSteps;
    }

    private generateSaccadeTargets(content: any): void {
        const pattern = typeof field(content, 'pattern') === 'string' ? field(content, 'pattern').toLowerCase() : 'horizontal';
        // 'type' conflicts with JS keyword, so we access it carefully. content.type is 'dot'|'letter' etc
        const contentType = typeof field(content, 'type') === 'string' ? field(content, 'type').toLowerCase() : 'dot';
        const pointSize = boundedInteger(field(content, 'pointSize'), 36, 8, 200);

        const targets = [];
        const count = 40; // Generate a batch to loop through

        // Content Generators
        const getVal = () => {
            if (contentType === 'dot') return '';
            if (contentType === 'letter') return 'ABCDEFGHIJKLMNOPRSTUVYZ'[Math.floor(Math.random() * 23)];
            if (contentType === 'number') return Math.floor(Math.random() * 10).toString();
            if (contentType === 'word') {
                const words = [
                    'OKU', 'HIZ', 'GÖZ', 'BAK', 'GÖR', 'NET', 'BİL', 'AL', 'AK', 'AS',
                    'AT', 'AZ', 'EL', 'EN', 'EV', 'ET', 'İŞ', 'İZ', 'İL', 'ON',
                    'OT', 'ÖN', 'ÖZ', 'ÜS', 'SU', 'YE', 'YOL', 'VAR', 'ÇOK', 'TEK'
                ];
                return words[Math.floor(Math.random() * words.length)];
            }
            return '';
        };

        if (pattern === 'horizontal') {
            for (let i = 0; i < count; i++) {
                targets.push({
                    x: i % 2 === 0 ? 10 : 90,
                    y: 50,
                    size: pointSize,
                    value: getVal()
                });
            }
        } else if (pattern === 'vertical') {
            for (let i = 0; i < count; i++) {
                targets.push({
                    x: 50,
                    y: i % 2 === 0 ? 10 : 90,
                    size: pointSize,
                    value: getVal()
                });
            }
        } else if (pattern === 'random') {
            for (let i = 0; i < count; i++) {
                targets.push({
                    x: 10 + Math.random() * 80,
                    y: 10 + Math.random() * 80,
                    size: pointSize,
                    value: getVal()
                });
            }
        } else if (pattern === 'z-pattern' || pattern === 'z-flow') {
            // Grid 3x3 or 4x5 lines
            const rows = 5;
            const cols = 2; // Left-Right
            for (let k = 0; k < count; k++) {
                const row = k % rows;
                const col = Math.floor(k / rows) % 2; // ZigZag logic could be simpler
                // Simple Z-flow: Left->Right, Down, Left->Right
                // Let's do simple line reading simulation
                const step = k % (rows * 2);
                const lineIndex = Math.floor(step / 2);
                const isLeft = step % 2 === 0;

                targets.push({
                    x: isLeft ? 10 : 90,
                    y: 15 + (lineIndex * (70 / (rows - 1))), // Distribute vertically
                    size: pointSize,
                    value: getVal()
                });
            }
        } else {
            // Default horizontal
            for (let i = 0; i < count; i++) {
                targets.push({ x: i % 2 === 0 ? 10 : 90, y: 50, size: pointSize, value: getVal() });
            }
        }

        this.saccadeTargets = targets;
    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.state.currentStep = 0;
        this.currentTargetIndex = 0;
        this.fixationResults = [];

        this.resetPosition();

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;

                if (!this.awaitingPeripheralInput) {
                    const holdMs = this.config.movement?.fixationTimeMs || 1000;
                    const elapsed = Date.now() - this.fixationStartTime;
                    this.fixationProgress = Math.min(100, (elapsed / holdMs) * 100);
                }

                // Time-based mode: calculate remaining seconds and check completion
                if (this.isTimeBased) {
                    // Timer update logic
                    const elapsedSeconds = Math.floor(this.state.timeElapsed / 1000);
                    this.state.remainingSeconds = Math.max(0, this.durationSeconds - elapsedSeconds);
                    this.state.isLastTenSeconds = this.state.remainingSeconds <= 10 && this.state.remainingSeconds > 0;
                    // Do NOT override currentStep with seconds. currentStep tracks points.


                    if (this.state.timeElapsed >= this.durationSeconds * 1000) {
                        this.complete('Time expired: ' + this.state.timeElapsed + ' >= ' + (this.durationSeconds * 1000));
                        return;
                    }
                }

                this.callbacks.onStateChange({ ...this.state });
            }
        }, 30);


        if (this.currentMode === 'fixation') {
            this.startFixation();
        } else if (this.currentMode === 'saccade') {
            this.startSaccade();
        } else {
            const pathType = this.config.path?.type;
            if (pathType === 'random_point' || pathType === 'two_point_jump') {
                this.startJumping();
            } else {
                this.startSmoothAnimation();
            }
        }

        this.callbacks.onStart();
        this.callbacks.onStateChange({ ...this.state });
    }

    private resetPosition(): void {
        const pathType = this.config.path?.type;
        if (pathType === 'horizontal') {
            this.targetX = 0; this.targetY = 50;
        } else if (pathType === 'vertical') {
            this.targetX = 50; this.targetY = 0;
        } else if (pathType === 'two_point_jump') {
            this.targetX = 20; this.targetY = 50;
        } else {
            this.targetX = 50;
            this.targetY = 50;
        }
    }

    // --- FIXATION MODE ---
    private startFixation(): void {
        this.showNextFixationPoint();
    }

    private showNextFixationPoint(): void {

        // Check completion: time-based is handled by timer, point-based by step count
        if (!this.isTimeBased && this.state.currentStep >= this.state.totalSteps) {
            this.complete('Max steps reached: ' + this.state.currentStep + ' >= ' + this.state.totalSteps);
            return;
        }

        // Time-based mode: continue until timer expires (handled in main interval)

        // Clear previous peripheral chars and prepare for new point
        this.peripheralChars = [];

        // Calculate new position
        this.pendingTargetX = 15 + Math.random() * 70;
        this.pendingTargetY = 20 + Math.random() * 60;
        this.scheduleFixationTransition(150);
    }

    private scheduleFixationTransition(delayMs: number): void {
        this.setFixationPhase('transition', delayMs);
        this.transitionTimeout = setTimeout(() => {
            if (!this.state.isRunning || this.state.isPaused) return;

            // Set new target position
            this.targetX = this.pendingTargetX;
            this.targetY = this.pendingTargetY;

            // Generate peripheral chars at new position
            this.generatePeripheralChars();

            // Start fixation timer
            this.fixationStartTime = Date.now();
            this.fixationProgress = 0;
            this.callbacks.onStateChange({ ...this.state });

            const holdMs = this.config.movement?.fixationTimeMs || 2000;
            this.scheduleFixationHold(holdMs);
        }, delayMs);
    }

    private scheduleFixationHold(delayMs: number): void {
        this.setFixationPhase('holding', delayMs);
        this.jumpInterval = setTimeout(() => {
            if (!this.state.isRunning || this.state.isPaused) return;
            this.completeFixationStep();
        }, delayMs);
    }

    private completeFixationStep(): void {
        // Play metronome sound if enabled
        if (this.useMetronome) {
            this.playMetronomeClick();
        }

        // If there are peripheral chars to test, wait for user input
        if (this.peripheralChars.length > 0) {
            this.fixationPhase = 'awaitingInput';
            this.awaitingPeripheralInput = true;
            this.currentCorrectChars = this.peripheralChars.map(p => p.char).join('');
            this.peripheralInputBuffer = '';
            this.totalPeripheralTests++;

            // Clear peripheral chars now that we're asking for input
            this.peripheralChars = [];
            this.callbacks.onStateChange({ ...this.state });
            // User input will be handled in handleInput()
        } else {
            this.fixationPhase = 'idle';
            // No peripheral chars - just record and move on
            this.recordFixationResult('', '', 100);
            this.state.currentStep++;
            this.callbacks.onStepComplete(this.state.currentStep, true);
            this.callbacks.onStateChange({ ...this.state });
            this.showNextFixationPoint();
        }
    }

    private generatePeripheralChars(): void {
        this.peripheralChars = [];
        const count = this.config.fixation?.peripheralCount || 0;

        if (count === 0) return;

        const allChars = 'ABCDEFGHKLMNPRSTVYZ';
        const positions: Array<'top' | 'bottom' | 'left' | 'right'> = ['top', 'bottom', 'left', 'right'];
        const usedPositions = positions.slice(0, count);

        // Filter out last used characters to avoid repetition
        let availableChars = allChars.split('').filter(c => !this.lastUsedChars.includes(c));

        // If not enough chars available (unlikely), reset and use all
        if (availableChars.length < count) {
            availableChars = allChars.split('');
        }

        // Fisher-Yates shuffle to get truly random unique characters
        for (let i = availableChars.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [availableChars[i], availableChars[j]] = [availableChars[j], availableChars[i]];
        }
        const selectedChars = availableChars.slice(0, count);

        // Store for next round
        this.lastUsedChars = [...selectedChars];


        usedPositions.forEach((pos, index) => {
            const char = selectedChars[index];
            let x = this.targetX;
            let y = this.targetY;
            const offset = 15 + Math.random() * 10;

            switch (pos) {
                case 'top': y = Math.max(5, this.targetY - offset); break;
                case 'bottom': y = Math.min(95, this.targetY + offset); break;
                case 'left': x = Math.max(5, this.targetX - offset); break;
                case 'right': x = Math.min(95, this.targetX + offset); break;
            }

            this.peripheralChars.push({ char, position: pos, x, y });
        });
    }

    // --- SACCADE MODE ---
    private startSaccade(): void {
        this.showNextSaccadeTarget();
    }

    private showNextSaccadeTarget(): void {


        // Fallback: Check if targets are empty (should have been generated in initialize)
        if (this.saccadeTargets.length === 0) {
            console.warn('Saccade Targets Empty! Attempting to regenerate...');
            const content = (this.config as any).engineConfig?.content || {};
            this.generateSaccadeTargets(content);
            if (this.saccadeTargets.length === 0) {
                console.error('Failed to regenerate saccade targets. Completing exercise.');
                this.complete('Saccade generation failed: targets still empty');
                return;
            }
        }

        // In time-based mode, loop back to start when targets are exhausted
        if (this.isTimeBased) {
            if (this.currentTargetIndex >= this.saccadeTargets.length) {
                this.currentTargetIndex = 0; // Loop back
            }
        } else {
            // Non-time-based: complete when all targets are shown
            if (this.currentTargetIndex >= this.saccadeTargets.length) {
                this.complete('All targets shown (Non-time-based): ' + this.currentTargetIndex + '/' + this.saccadeTargets.length);
                return;
            }
        }

        const target = this.saccadeTargets[this.currentTargetIndex];
        // Case-insensitive property access for backend targets
        this.targetX = target.X !== undefined ? target.X : target.x;
        this.targetY = target.Y !== undefined ? target.Y : target.y;
        this.currentPointSize = target.Size || target.size || 30;
        this.state.currentValue = target.Value || target.value || '';

        this.fixationStartTime = Date.now();
        this.fixationProgress = 0;
        this.callbacks.onStateChange({ ...this.state });

        const holdMs = this.config.movement?.fixationTimeMs || 1000;

        this.jumpInterval = setTimeout(() => {
            if (!this.state.isRunning || this.state.isPaused) return;
            this.onTargetAction();
        }, holdMs);
    }

    private onTargetAction(): void {
        if (this.jumpInterval) clearTimeout(this.jumpInterval);

        const responseTime = Date.now() - this.fixationStartTime;
        const target = this.saccadeTargets[this.currentTargetIndex];

        this.callbacks.onAction({
            action: 'target_clicked',
            number: target.Number || target.number,
            responseTime: responseTime,
            timestamp: new Date().toISOString()
        });

        this.currentTargetIndex++;
        this.saccadeInputLocked = true;

        // Track completed targets count for display
        this.state.targetCount = (this.state.targetCount || 0) + 1;

        // In time-based mode, currentStep is managed by the timer
        if (!this.isTimeBased) {
            this.state.currentStep = this.currentTargetIndex;
        }
        this.callbacks.onStepComplete(this.currentTargetIndex, true);

        this.showNextSaccadeTarget();
        this.saccadeUnlockTimeout = setTimeout(() => this.saccadeInputLocked = false, 50);
    }

    // --- SMOOTH ANIMATION ---
    private startSmoothAnimation(): void {
        this.animate();
    }

    private animate(): void {
        if (!this.state.isRunning || this.state.isPaused) return;

        const pathType = this.config.path?.type;
        const speedLevel = this.config.movement?.speedLevel || 1;

        if (pathType === 'horizontal') {
            const speed = 0.5 + (speedLevel * 0.3);
            this.targetX += speed * this.direction;
            if (this.targetX >= 95 || this.targetX <= 5) {
                this.direction *= -1;
                this.state.currentStep++;
                if (this.completeTrackingIfNeeded()) return;
            }
        }
        else if (pathType === 'vertical') {
            const speed = 0.5 + (speedLevel * 0.3);
            this.targetY += speed * this.direction;
            if (this.targetY >= 90 || this.targetY <= 10) {
                this.direction *= -1;
                this.state.currentStep++;
                if (this.completeTrackingIfNeeded()) return;
            }
        }
        else if (pathType === 'circle') {
            const speed = 0.02 + (speedLevel * 0.01);
            const previousCycle = Math.floor(this.angle / (Math.PI * 2));
            this.angle += speed;
            const radius = 35;
            this.targetX = 50 + radius * Math.cos(this.angle);
            this.targetY = 50 + radius * Math.sin(this.angle);
            if (Math.floor(this.angle / (Math.PI * 2)) > previousCycle) {
                this.state.currentStep++;
                if (this.completeTrackingIfNeeded()) return;
            }
        }
        else if (pathType === 'infinity8') {
            const speed = 0.03 + (speedLevel * 0.01);
            const previousCycle = Math.floor(this.angle / (Math.PI * 2));
            this.angle += speed;
            const scale = 2 / (3 - Math.cos(2 * this.angle));
            const x = scale * Math.cos(this.angle);
            const y = scale * Math.sin(2 * this.angle) / 2;
            this.targetX = 50 + (x * 40);
            this.targetY = 50 + (y * 40);
            if (Math.floor(this.angle / (Math.PI * 2)) > previousCycle) {
                this.state.currentStep++;
                if (this.completeTrackingIfNeeded()) return;
            }
        }

        this.callbacks.onStateChange({ ...this.state });
        this.animationFrame = requestAnimationFrame(() => this.animate());
    }

    // --- JUMPING ---
    private startJumping(jumpImmediately = true): void {
        const interval = this.config.movement?.jumpIntervalMs || 1000;
        if (jumpImmediately) this.jump();
        if (!this.state.isRunning) return;
        this.jumpInterval = setInterval(() => {
            if (!this.state.isPaused) this.jump();
        }, interval);
    }

    private jump(): void {
        const pathType = this.config.path?.type;
        if (pathType === 'random_point') {
            this.targetX = 10 + Math.random() * 80;
            this.targetY = 10 + Math.random() * 80;
        } else if (pathType === 'two_point_jump') {
            this.targetX = (this.targetX <= 30) ? 80 : 20;
            this.targetY = 50;
        }
        this.state.currentStep++;
        this.callbacks.onStateChange({ ...this.state });
        this.completeTrackingIfNeeded();
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        if (this.animationFrame) cancelAnimationFrame(this.animationFrame);
        if (this.jumpInterval) {
            clearTimeout(this.jumpInterval);
            clearInterval(this.jumpInterval);
        }
        if (this.transitionTimeout) clearTimeout(this.transitionTimeout);
        if (this.feedbackTimeout) clearTimeout(this.feedbackTimeout);
        if (this.currentMode === 'fixation' && ['transition', 'holding', 'feedback'].includes(this.fixationPhase)) {
            this.phaseRemaining = Math.max(0, this.phaseDuration - (Date.now() - this.phaseStartedAt));
        }
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust timers
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;
        this.fixationStartTime += pauseDuration;

        this.state.isPaused = false;

        // Resume mode-specific logic
        if (this.currentMode === 'fixation') {
            if (this.fixationPhase === 'transition') this.scheduleFixationTransition(this.phaseRemaining);
            else if (this.fixationPhase === 'holding') this.scheduleFixationHold(this.phaseRemaining);
            else if (this.fixationPhase === 'feedback') this.scheduleFeedback(this.phaseRemaining);

        } else if (this.currentMode === 'saccade') {
            const holdMs = this.config.movement?.fixationTimeMs || 1000;
            const remaining = Math.max(0, holdMs - (Date.now() - this.fixationStartTime));

            this.jumpInterval = setTimeout(() => {
                if (!this.state.isRunning || this.state.isPaused) return;
                this.onTargetAction();
            }, remaining);

        } else {
            const pathType = this.config.path?.type;
            if (pathType === 'random_point' || pathType === 'two_point_jump') this.startJumping(false);
            else this.startSmoothAnimation();
        }

        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.jumpInterval);
        clearTimeout(this.transitionTimeout);
        clearTimeout(this.feedbackTimeout);
        clearTimeout(this.saccadeUnlockTimeout);
        if (this.animationFrame) cancelAnimationFrame(this.animationFrame);
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.configuredTotalSteps,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0,
            currentValue: ''
        };
        this.fixationProgress = 0;
        this.peripheralChars = [];
        this.fixationResults = [];
        this.awaitingPeripheralInput = false;
        this.currentCorrectChars = '';
        this.peripheralInputBuffer = '';
        this.totalPeripheralTests = 0;
        this.correctPeripheralTests = 0;
        this.currentTargetIndex = 0;
        this.saccadeInputLocked = false;
        this.lastFeedback = null;
        this.showingFeedback = false;
        this.fixationPhase = 'idle';
        this.resetPosition();
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    handleInput(input: any): void {
        const mode = this.currentMode;

        // Handle peripheral vision input
        if (this.awaitingPeripheralInput && input.type === 'keypress') {
            const key = input.key?.toUpperCase();
            if (key && key.length === 1 && /[A-Z]/.test(key)) {
                this.peripheralInputBuffer += key;

                // Check if we have enough characters
                if (this.peripheralInputBuffer.length >= this.currentCorrectChars.length) {
                    this.submitPeripheralInput();
                }
            }
            return;
        }

        // Handle Enter to submit peripheral input early
        if (this.awaitingPeripheralInput && (input.type === 'enter' || input.key === 'Enter')) {
            this.submitPeripheralInput();
            return;
        }

        // Handle saccade clicks
        if (input.type === 'click' || input.type === 'space') {
            if (mode === 'saccade' && this.state.isRunning && !this.state.isPaused && !this.saccadeInputLocked) {
                this.onTargetAction();
            }
        }
    }

    private submitPeripheralInput(): void {
        const userInput = this.peripheralInputBuffer.toUpperCase();
        const correctChars = this.currentCorrectChars.toUpperCase();

        // Calculate accuracy (character-by-character match)
        let matchCount = 0;
        const remainingChars = correctChars.split('');
        const minLen = Math.min(userInput.length, correctChars.length);
        for (let i = 0; i < minLen; i++) {
            // Check if character exists in correct chars (order doesn't matter for peripheral vision)
            const matchIndex = remainingChars.indexOf(userInput[i]);
            if (matchIndex >= 0) {
                matchCount++;
                remainingChars.splice(matchIndex, 1);
            }
        }
        const accuracy = correctChars.length > 0 ? (matchCount / correctChars.length) * 100 : 100;
        const isCorrect = accuracy === 100;

        if (isCorrect) {
            this.correctPeripheralTests++;
        }

        this.recordFixationResult(userInput, correctChars, accuracy);

        // Update score based on peripheral accuracy
        this.state.accuracy = this.fixationResults.length > 0
            ? this.fixationResults.reduce((sum, item) => sum + item.accuracy, 0) / this.fixationResults.length
            : 100;

        this.awaitingPeripheralInput = false;

        // Show feedback
        this.lastFeedback = { isCorrect, userInput, correctChars };
        this.showingFeedback = true;
        this.callbacks.onStateChange({ ...this.state });

        // Wait 1.2 seconds to show feedback, then continue
        this.scheduleFeedback(1200);
    }

    private scheduleFeedback(delayMs: number): void {
        this.setFixationPhase('feedback', delayMs);
        this.feedbackTimeout = setTimeout(() => {
            if (!this.state.isRunning || this.state.isCompleted) return;
            const isCorrect = this.lastFeedback?.isCorrect ?? false;
            this.showingFeedback = false;
            this.lastFeedback = null;
            this.fixationPhase = 'idle';

            this.state.currentStep++;
            this.callbacks.onStepComplete(this.state.currentStep, isCorrect);
            this.callbacks.onStateChange({ ...this.state });

            this.showNextFixationPoint();
        }, delayMs);
    }

    private recordFixationResult(userInput: string, correctChars: string, accuracy: number): void {
        this.fixationResults.push({
            pointIndex: this.state.currentStep,
            peripheralChars: [...this.peripheralChars],
            userInput,
            correctChars,
            accuracy,
            fixationTimeMs: this.config.movement?.fixationTimeMs || 2000,
            timestamp: new Date().toISOString()
        });
    }

    private playMetronomeClick(): void {
        try {
            if (!this.audioContext) {
                this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
            }
            const oscillator = this.audioContext.createOscillator();
            const gainNode = this.audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(this.audioContext.destination);

            oscillator.frequency.value = 880; // A5 note
            oscillator.type = 'sine';

            gainNode.gain.setValueAtTime(0.3, this.audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, this.audioContext.currentTime + 0.1);

            oscillator.start(this.audioContext.currentTime);
            oscillator.stop(this.audioContext.currentTime + 0.1);
        } catch (e) {
            console.warn('Metronome audio not available:', e);
        }
    }

    isAwaitingInput(): boolean {
        return this.awaitingPeripheralInput;
    }

    getPeripheralInputBuffer(): string {
        return this.peripheralInputBuffer;
    }

    getPeripheralAccuracy(): number {
        return this.state.accuracy;
    }

    isShowingFeedback(): boolean {
        return this.showingFeedback;
    }

    getFeedback(): { isCorrect: boolean; userInput: string; correctChars: string } | null {
        return this.lastFeedback;
    }

    getCorrectCount(): number {
        return this.correctPeripheralTests;
    }

    getIncorrectCount(): number {
        return this.totalPeripheralTests - this.correctPeripheralTests;
    }

    private complete(reason: string = 'unknown'): void {
        if (this.state.isCompleted) return;
        const completedAccuracy = this.fixationResults.reduce((sum, item) => sum + item.accuracy, 0);
        const accuracy = this.totalPeripheralTests > 0
            ? Math.round(completedAccuracy / this.totalPeripheralTests)
            : 100;
        const errors = this.getIncorrectCount();
        this.state.score = accuracy;
        this.state.accuracy = accuracy;
        this.state.errors = errors;
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.stop();

        const result: EngineResult = {
            score: accuracy,
            accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.currentStep,
            errors,
            details: {
                fixationResults: this.fixationResults
            }
        };

        this.callbacks.onStateChange({ ...this.state });
        this.callbacks.onComplete(result);
    }

    private completeTrackingIfNeeded(): boolean {
        if (!this.isTimeBased && this.state.currentStep >= this.state.totalSteps) {
            this.complete('Tracking steps completed');
            return true;
        }
        return false;
    }

    private normalizeTargetNumber(value: unknown): string | number | undefined {
        if (typeof value === 'number' && Number.isSafeInteger(value)) return value;
        if (typeof value === 'string') return boundedText(value, '', 100) || undefined;
        return undefined;
    }

    private setFixationPhase(phase: 'transition' | 'holding' | 'feedback', durationMs: number): void {
        this.fixationPhase = phase;
        this.phaseStartedAt = Date.now();
        this.phaseDuration = durationMs;
        this.phaseRemaining = durationMs;
    }

    // Public API
    getTargetPosition() { return { x: this.targetX, y: this.targetY }; }
    getTargetConfig() {
        return {
            type: this.config.target?.type || 'dot',
            size: this.config.target?.size || 'medium',
            color: this.config.target?.color || 'primary'
        };
    }
    getFixationResults() { return this.fixationResults; }
    getFixationProgress() { return this.fixationProgress; }
    getPeripheralChars() { return this.peripheralChars; }
    getPointSize() { return this.currentPointSize; }
    getCurrentValue() { return this.state.currentValue; }
    getFixationDuration() { return this.config.movement?.fixationTimeMs || 0; }
}
