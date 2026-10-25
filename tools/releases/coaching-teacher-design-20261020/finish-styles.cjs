module.exports = function finishStyles(ast) {
  ast.walkRules(rule => {
    const selectors = rule.selector.split(',');
    for (const [pattern, text, bg] of [
      [/error|danger|overdue|negative/i, 'var(--ui-danger)', 'var(--coaching-danger-soft)'],
      [/success|positive|\.paid\b|\.completed\b/i, 'var(--ui-success)', 'var(--ui-brand-soft)'],
      [/warning|\.late\b|pending|\.status\b/i, 'var(--coaching-warning)', 'var(--coaching-warm)']
    ]) {
      if (selectors.some(s => pattern.test(s)) && !selectors.every(s => pattern.test(s))) {
        rule.walkDecls(d => { if (d.prop === 'color' && d.value === text) d.value = 'var(--ui-text)'; if (/^background(-color)?$/.test(d.prop) && d.value === bg) d.value = 'var(--ui-surface)'; });
      }
    }
    if (rule.nodes.some(d => /^background(-color)?$/.test(d.prop) && d.value === 'var(--ui-brand)')) {
      rule.walkDecls('color', d => { if (d.value === 'var(--ui-text)') d.value = '#fff'; });
    }
  });
  return ast.toString();
};
