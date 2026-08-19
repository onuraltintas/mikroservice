/** Permission keys shared with the Identity service. */
export const ADMIN_PERMISSIONS = {
  usersView: 'Permissions.Users.View',
  rolesView: 'Permissions.Roles.View',
  permissionView: 'Permissions.Permissions.View',
  institutionsView: 'Permissions.Institutions.View',
  institutionsManage: 'Permissions.Institutions.Manage',
  coachingView: 'Permissions.Coaching.View',
  coachingManage: 'Permissions.Coaching.Manage',
  supportView: 'Permissions.Support.View',
  supportReply: 'Permissions.Support.Reply',
  notificationTemplates: 'Permissions.Notifications.Templates',
  operationsView: 'Permissions.Operations.View'
} as const;
