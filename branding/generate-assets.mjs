// Generates every MSIX visual asset and the Win32 app.ico from the SVG logos.
// Usage (from this folder):  npm install  &&  npm run assets
import { Resvg } from "@resvg/resvg-js";
import pngToIco from "png-to-ico";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, "..", "src", "Vistrel.App", "Assets");
const storeDir = join(here, "store");
mkdirSync(outDir, { recursive: true });
mkdirSync(storeDir, { recursive: true });

const logo = readFileSync(join(here, "logo.svg"), "utf8");
const logoSmall = readFileSync(join(here, "logo-small.svg"), "utf8");
const inner = (svg) => svg.replace(/^[\s\S]*?<svg[^>]*>/, "").replace(/<\/svg>\s*$/, "");

/** Renders the logo centred on a transparent canvas of w x h, with the logo `fill` (0-1) of the shorter side. */
function render(w, h, fill) {
  const src = Math.min(w, h) * fill <= 48 ? logoSmall : logo;
  const size = Math.round(Math.min(w, h) * fill);
  const x = Math.round((w - size) / 2), y = Math.round((h - size) / 2);
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
    <svg x="${x}" y="${y}" width="${size}" height="${size}" viewBox="0 0 256 256">${inner(src)}</svg></svg>`;
  return new Resvg(svg, { fitTo: { mode: "original" }, background: "rgba(0,0,0,0)" }).render().asPng();
}

const scales = [100, 125, 150, 200, 400];
const assets = [
  // [base name, width, height, logo fill]
  ["Square44x44Logo", 44, 44, 0.92],
  ["Square71x71Logo", 71, 71, 0.62],
  ["Square150x150Logo", 150, 150, 0.6],
  ["Wide310x150Logo", 310, 150, 0.62],
  ["Square310x310Logo", 310, 310, 0.55],
  ["StoreLogo", 50, 50, 0.92],
  ["SplashScreen", 620, 300, 0.5],
];
let count = 0;
for (const [name, w, h, fill] of assets) {
  for (const s of scales) {
    const W = Math.round((w * s) / 100), H = Math.round((h * s) / 100);
    writeFileSync(join(outDir, `${name}.scale-${s}.png`), render(W, H, fill));
    count++;
  }
}
// Taskbar / Start / Explorer icons at exact pixel sizes (plated, unplated, light-unplated).
for (const t of [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]) {
  const png = render(t, t, t <= 24 ? 1 : 0.94);
  for (const suffix of ["", "_altform-unplated", "_altform-lightunplated"]) {
    writeFileSync(join(outDir, `Square44x44Logo.targetsize-${t}${suffix}.png`), png);
    count++;
  }
}
// Win32 icon for the window title bar and the unpackaged build.
const icoPngs = [16, 24, 32, 48, 64, 256].map((t) => render(t, t, t <= 24 ? 1 : 0.94));
writeFileSync(join(outDir, "app.ico"), await pngToIco(icoPngs));
// Store listing images.
writeFileSync(join(storeDir, "store-logo-300.png"), render(300, 300, 0.9));
writeFileSync(join(storeDir, "store-logo-1080.png"), render(1080, 1080, 0.9));
console.log(`Wrote ${count} MSIX assets + app.ico to ${outDir}, store images to ${storeDir}`);
