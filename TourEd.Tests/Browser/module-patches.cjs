// Shared helper for the browser regression tests: serves the real frontend modules
// with test-only patches. Each rule must match exactly once across all modules, so a
// patch can never silently stop applying after code moves between modules.
const { readdirSync, readFileSync } = require('node:fs');
const { join } = require('node:path');

/**
 * @param {string} root wwwroot directory
 * @param {{name: string, find: (string|RegExp)[], replace: string}[]} rules applied in order;
 *        the first matching alternative of `find` is replaced (first occurrence only)
 * @param {string|undefined} entryOverride optional replacement for js/toured.js (negative controls)
 * @returns {Map<string, string>} patched sources keyed by path relative to wwwroot
 */
function loadPatchedModules(root, rules, entryOverride) {
    const sources = new Map(readdirSync(join(root, 'js'))
        .filter(name => name.endsWith('.js'))
        .map(name => [`js/${name}`, readFileSync(join(root, 'js', name), 'utf8')]));
    if (entryOverride) sources.set('js/toured.js', readFileSync(entryOverride, 'utf8'));
    for (const rule of rules) {
        const hits = [];
        for (const [file, text] of sources) {
            for (const find of rule.find) {
                const matched = typeof find === 'string' ? text.includes(find) : find.test(text);
                if (matched) {
                    sources.set(file, text.replace(find, rule.replace));
                    hits.push(file);
                    break;
                }
            }
        }
        if (hits.length !== 1) {
            throw new Error(`Patch rule "${rule.name}" matched ${hits.length} modules (${hits.join(', ')}); expected exactly one.`);
        }
    }
    return sources;
}

module.exports = { loadPatchedModules };
