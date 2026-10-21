import { copyFile, cp, mkdir } from 'node:fs/promises';
// Keep the served files and their licenses aligned with package-lock.json.
for (const [source, target, license] of [
  ['@fortawesome/fontawesome-free', 'fontawesome', 'LICENSE.txt'],
  ['@fontsource/cairo', 'fontsource-cairo', 'LICENSE'],
  ['sweetalert2', 'sweetalert2', 'LICENSE']
]) {
  const destination = `wwwroot/lib/${target}`;
  await mkdir(destination, { recursive: true });
  await copyFile(`node_modules/${source}/${license}`, `${destination}/${license}`);
}
await mkdir('wwwroot/lib/fontawesome/css', { recursive: true });
await copyFile('node_modules/@fortawesome/fontawesome-free/css/all.min.css', 'wwwroot/lib/fontawesome/css/all.min.css');
await cp('node_modules/@fortawesome/fontawesome-free/webfonts', 'wwwroot/lib/fontawesome/webfonts', { recursive: true });
for (const weight of ['400', '700']) {
  await copyFile(`node_modules/@fontsource/cairo/${weight}.css`, `wwwroot/lib/fontsource-cairo/${weight}.css`);
}
// Copy only font files referenced by the two included stylesheets.
import { readFile } from 'node:fs/promises';
await mkdir('wwwroot/lib/fontsource-cairo/files', { recursive: true });
for (const weight of ['400', '700']) {
  const css = await readFile(`node_modules/@fontsource/cairo/${weight}.css`, 'utf8');
  for (const match of css.matchAll(/url\(\.\/files\/([^)]*)\)/g))
    await copyFile(`node_modules/@fontsource/cairo/files/${match[1]}`, `wwwroot/lib/fontsource-cairo/files/${match[1]}`);
}
await copyFile('node_modules/sweetalert2/dist/sweetalert2.all.min.js', 'wwwroot/lib/sweetalert2/sweetalert2.all.min.js');
