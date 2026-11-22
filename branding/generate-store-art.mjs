// Generates the optional Microsoft Store listing images (Partner Center → Store listings → Store logos,
// Store display images, and the 16:9 Super hero art) from the logo.
// Usage (from this folder):  npm install  &&  npm run store-art
import { Resvg } from "@resvg/resvg-js";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, "store");
mkdirSync(outDir, { recursive: true });

const inner = (svg) => svg.replace(/^[\s\S]*?<svg[^>]*>/, "").replace(/<\/svg>\s*$/, "");
const logo = inner(readFileSync(join(here, "logo.svg"), "utf8"));
const logoSmall = inner(readFileSync(join(here, "logo-small.svg"), "utf8"));

// Brand palette (matches the app's chrome and accent).
const C = { cyan: "#00E5FF", blue: "#2962FF", violet: "#7A3CFF", ink: "#0B1426", chrome: "#12151B", text: "#F2F6FC", muted: "#9AA4B5" };
const FONT = "Segoe UI Variable Display, Segoe UI, sans-serif";

const background = (w, h) => `
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${C.ink}"/><stop offset="1" stop-color="${C.chrome}"/>
    </linearGradient>
    <radialGradient id="glowA" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="${C.cyan}" stop-opacity="0.55"/><stop offset="1" stop-color="${C.cyan}" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="glowB" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="${C.blue}" stop-opacity="0.6"/><stop offset="1" stop-color="${C.blue}" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="glowC" cx="0.5" cy="0.5" r="0.5">
      <stop offset="0" stop-color="${C.violet}" stop-opacity="0.35"/><stop offset="1" stop-color="${C.violet}" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect width="${w}" height="${h}" fill="url(#bg)"/>`;

/** Concentric lens rings echoing the logo, as a subtle background motif. */
const rings = (cx, cy, r, opacity = 0.12) => [1, 1.35, 1.75, 2.2]
  .map((k, i) => `<circle cx="${cx}" cy="${cy}" r="${r * k}" fill="none" stroke="${C.cyan}" stroke-opacity="${opacity / (i + 1)}" stroke-width="${Math.max(2, r * 0.012)}"/>`)
  .join("");

const placeLogo = (x, y, size, small = false) =>
  `<svg x="${x}" y="${y}" width="${size}" height="${size}" viewBox="0 0 256 256">${small ? logoSmall : logo}</svg>`;

const title = (x, y, size, anchor = "middle") =>
  `<text x="${x}" y="${y}" font-family="${FONT}" font-weight="600" font-size="${size}" fill="${C.text}" text-anchor="${anchor}" letter-spacing="${-size * 0.01}">Vistrel Player</text>`;

const tagline = (x, y, size, anchor = "middle") =>
  `<text x="${x}" y="${y}" font-family="${FONT}" font-weight="400" font-size="${size}" fill="${C.muted}" text-anchor="${anchor}">Fast. Private. Every format.</text>`;

const art = {
  // 9:16 Poster art (main logo on Windows 10/11; required for Xbox). Includes the name.
  "poster-art-1440x2160.png": [1440, 2160, (w, h) => `
    ${background(w, h)}
    <ellipse cx="${w * 0.5}" cy="${h * 0.36}" rx="${w * 0.7}" ry="${w * 0.7}" fill="url(#glowB)"/>
    <ellipse cx="${w * 0.72}" cy="${h * 0.2}" rx="${w * 0.45}" ry="${w * 0.45}" fill="url(#glowA)"/>
    ${rings(w * 0.5, h * 0.37, 330)}
    ${placeLogo(w * 0.5 - 330, h * 0.37 - 330, 660)}
    ${title(w / 2, h * 0.68, 150)}
    ${tagline(w / 2, h * 0.68 + 110, 64)}`],

  // 1:1 Box art. Includes the name.
  "box-art-2160x2160.png": [2160, 2160, (w, h) => `
    ${background(w, h)}
    <ellipse cx="${w * 0.5}" cy="${h * 0.4}" rx="${w * 0.55}" ry="${w * 0.55}" fill="url(#glowB)"/>
    <ellipse cx="${w * 0.7}" cy="${h * 0.25}" rx="${w * 0.35}" ry="${w * 0.35}" fill="url(#glowA)"/>
    ${rings(w * 0.5, h * 0.4, 420)}
    ${placeLogo(w * 0.5 - 420, h * 0.4 - 420, 840)}
    ${title(w / 2, h * 0.8, 200)}
    ${tagline(w / 2, h * 0.8 + 140, 84)}`],

  // 16:9 Super hero art (top of the listing). Must NOT include the product name.
  "super-hero-art-3840x2160.png": [3840, 2160, (w, h) => `
    ${background(w, h)}
    <ellipse cx="${w * 0.68}" cy="${h * 0.5}" rx="${h * 0.95}" ry="${h * 0.95}" fill="url(#glowB)"/>
    <ellipse cx="${w * 0.82}" cy="${h * 0.25}" rx="${h * 0.6}" ry="${h * 0.6}" fill="url(#glowA)"/>
    <ellipse cx="${w * 0.2}" cy="${h * 0.85}" rx="${h * 0.7}" ry="${h * 0.5}" fill="url(#glowC)"/>
    ${rings(w * 0.68, h * 0.5, 520, 0.18)}
    ${placeLogo(w * 0.68 - 520, h * 0.5 - 520, 1040)}`],

  // Store display images (1:1 tiles on Windows 10/11): logo on the app's dark chrome.
  ...Object.fromEntries([300, 150, 71].map((s) => [`app-tile-${s}x${s}.png`, [s, s, (w, h) => `
    <rect width="${w}" height="${h}" fill="${C.chrome}"/>
    ${placeLogo(w * 0.15, h * 0.15, w * 0.7, s <= 71)}`]])),
};

for (const [name, [w, h, body]] of Object.entries(art)) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body(w, h)}</svg>`;
  const png = new Resvg(svg, {
    fitTo: { mode: "original" },
    font: { loadSystemFonts: true, defaultFontFamily: "Segoe UI" },
  }).render().asPng();
  writeFileSync(join(outDir, name), png);
  console.log(`${name.padEnd(30)} ${w}x${h}  ${(png.length / 1024).toFixed(0)} KB`);
}
