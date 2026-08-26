import { readFile } from 'node:fs/promises';

const load = async (name) => JSON.parse(await readFile(new URL(`../src/i18n/locales/${name}.json`, import.meta.url), 'utf8'));
const [ar, en] = await Promise.all([load('ar'), load('en')]);
const arKeys = Object.keys(ar).sort();
const enKeys = Object.keys(en).sort();
const missingEnglish = arKeys.filter((key) => !(key in en));
const missingArabic = enKeys.filter((key) => !(key in ar));
const empty = [...new Set([...arKeys, ...enKeys])].filter((key) => !String(ar[key] ?? '').trim() || !String(en[key] ?? '').trim());

if (missingEnglish.length || missingArabic.length || empty.length) {
  console.error(JSON.stringify({ missingEnglish, missingArabic, empty }, null, 2));
  process.exit(1);
}

console.log(`Translation contract passed: ${arKeys.length} Arabic/English keys.`);
