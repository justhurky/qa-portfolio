const MIN_DURATION_MINUTES = 5;
const MAX_DURATION_MINUTES = 120;
const MIN_LEAD_MINUTES = 15;
const MAX_LEAD_DAYS = 30;
const MIN_PURPOSE_LENGTH = 3;
const MAX_PURPOSE_LENGTH = 200;

const ROLES = ['VIEWER', 'OPERATOR', 'SUPERVISOR', 'ADMIN'];
const PRIORITIES = ['ROUTINE', 'PRIORITY', 'EMERGENCY'];
const STATES = ['REQUESTED', 'CONFIRMED', 'COMPLETED', 'CANCELLED'];

const ALLOWED_TRANSITIONS = {
  REQUESTED: ['CONFIRMED', 'CANCELLED'],
  CONFIRMED: ['COMPLETED', 'CANCELLED'],
  COMPLETED: [],
  CANCELLED: []
};

const PRIORITY_RULES = {
  ROUTINE: { minimumRole: 'OPERATOR', mayOverrideLeadTime: false },
  PRIORITY: { minimumRole: 'OPERATOR', mayOverrideLeadTime: false },
  EMERGENCY: { minimumRole: 'SUPERVISOR', mayOverrideLeadTime: true }
};

function roleRank(role) {
  return ROLES.indexOf(role);
}

function hasAtLeastRole(role, minimumRole) {
  return roleRank(role) >= roleRank(minimumRole);
}

function canTransition(from, to) {
  return (ALLOWED_TRANSITIONS[from] || []).includes(to);
}

function parseInstant(value) {
  if (typeof value !== 'string' || value.trim() === '') {
    return null;
  }
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? null : new Date(parsed);
}

function durationMinutes(startsAt, endsAt) {
  return (endsAt.getTime() - startsAt.getTime()) / 60000;
}

function leadMinutes(startsAt, now) {
  return (startsAt.getTime() - now.getTime()) / 60000;
}

function overlaps(first, second) {
  return first.startsAt < second.endsAt && second.startsAt < first.endsAt;
}

module.exports = {
  MIN_DURATION_MINUTES,
  MAX_DURATION_MINUTES,
  MIN_LEAD_MINUTES,
  MAX_LEAD_DAYS,
  MIN_PURPOSE_LENGTH,
  MAX_PURPOSE_LENGTH,
  ROLES,
  PRIORITIES,
  STATES,
  ALLOWED_TRANSITIONS,
  PRIORITY_RULES,
  roleRank,
  hasAtLeastRole,
  canTransition,
  parseInstant,
  durationMinutes,
  leadMinutes,
  overlaps
};
