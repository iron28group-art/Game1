// 헤드리스 자동 플레이 시험: node tools/playtest.js [판 수] [스크린샷 폴더]
// 봇이 가까운 적(조준 중인 저격수 우선)을 오차 있게 조준해 쏘고, 카드를 무작위로 고른다.
const { chromium } = require('playwright');
const path = require('path');
const runs = +(process.argv[2] || 6);
const shotDir = process.argv[3] || '';
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 432, height: 768 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto('file://' + path.resolve(__dirname, '..', 'index.html'));
  await page.waitForTimeout(800);
  if (shotDir) await page.screenshot({ path: shotDir + '/title.png' });
  const results = [];
  for (let r = 0; r < runs; r++) {
    const skill = [0.6, 1.0, 1.4][r % 3]; // 오차 배율 (작을수록 잘 쏨)
    const res = await page.evaluate(({ skill, wantShots }) => {
      const G = window.__gd;
      G.start();
      const dt = 1 / 60;
      let t = 0, shots = {};
      const gauss = () => { let u = 0, v = 0; while (!u) u = Math.random(); while (!v) v = Math.random(); return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v); };
      let think = 0;
      while (t < 60 * 30) {
        const sc = G.scene;
        if (sc === 'levelup') { G.chooseCard((Math.random() * G.cards.length) | 0); continue; }
        if (sc !== 'play') break;
        const g = G.g, p = G.p;
        think -= dt;
        if (p.reloading >= 0 && !p.reloadTried && p.reloading / p.reloadTime > 0.5 && Math.random() < 0.5 * dt * 60 / 20) G.tryActiveReload();
        if (p.fanCharge >= 1) G.useFan();
        if (think <= 0) {
          const live = g.enemies.filter(e => G.alive(e) && e.z < 100);
          if (live.length) {
            live.sort((a, b) => ((a.def.ranged && a.state === 'aim' ? -60 : 0) + a.z) - ((b.def.ranged && b.state === 'aim' ? -60 : 0) + b.z));
            const e = live[0];
            const wantScope = e.z > 40;
            if (wantScope !== p.scoped) G.toggleScope();
            const P = G.parts(e);
            const aimHead = P.head && Math.random() < 0.5;
            let tx, ty;
            if (P.weak) { tx = P.weak[0]; ty = P.weak[1]; }
            else if (aimHead) { tx = P.head[0]; ty = P.head[1]; }
            else { tx = P.body[0] + P.body[2] / 2; ty = P.body[1] + P.body[3] * 0.4; }
            const err = (p.scoped ? 4 : 10) * skill;
            p.cx = tx + gauss() * err; p.cy = ty + gauss() * err;
            if (G.fire()) think = 0.55 + Math.random() * 0.35; // 사람 반응 시간
          }
        }
        G.update(dt);
        t += dt;
        if (wantShots && !shots.a && g.wave === 3 && g.phase === 'fight' && g.enemies.length > 4) { shots.a = 1; G.render(); window.__snapA = true; break; }
      }
      const g = G.g;
      return { skill, wave: g.wave, scene: G.scene, lvl: g.lvl, kills: g.kills, heads: g.heads, acc: g.shots ? Math.round(g.hits / g.shots * 100) : 0, hp: Math.round(g.hp), time: Math.round(t), cards: JSON.stringify(G.p.cards) };
    }, { skill, wantShots: false });
    results.push(res);
    console.log(JSON.stringify(res));
  }
  if (shotDir) {
    // 장면 스크린샷: 3웨이브 전투, 스코프, 레벨업
    await page.evaluate(() => {
      const G = window.__gd; G.start();
      for (let i = 0; i < 60 * 40 && G.g.wave < 2; i++) { if (G.scene === 'levelup') G.chooseCard(0); for (const e of G.g.enemies) if (e.z < 60) e.hp = 0, e.dead = true, e.dieT = 0; G.update(1 / 60); }
      for (let i = 0; i < 60 * 14; i++) { if (G.scene === 'levelup') G.chooseCard(0); G.update(1 / 60); }
      G.scene = 'pause';
    });
    await page.evaluate(() => { const G = window.__gd; G.scene = 'play'; const e = G.g.enemies.filter(G.alive).sort((a, b) => a.z - b.z)[0]; if (e) { const P = G.parts(e); G.p.cx = P.head[0]; G.p.cy = P.head[1]; } G.scene = 'pause'; G.render(); });
    await page.evaluate(() => { window.__gd.scene = 'play'; });
    await page.waitForTimeout(50);
    await page.evaluate(() => { window.__gd.scene = 'pause'; window.__gd.scene = 'play'; });
    await page.screenshot({ path: shotDir + '/play.png' });
    await page.evaluate(() => { const G = window.__gd; const e = G.g.enemies.filter(G.alive).sort((a, b) => b.z - a.z)[0]; if (e) { const P = G.parts(e); G.p.cx = P.head[0]; G.p.cy = P.head[1]; } G.toggleScope(); });
    await page.waitForTimeout(400);
    await page.screenshot({ path: shotDir + '/scope.png' });
    await page.evaluate(() => { const G = window.__gd; G.toggleScope(); G.g.pendingLv = 1; G.update(1 / 60); });
    await page.waitForTimeout(100);
    await page.screenshot({ path: shotDir + '/levelup.png' });
  }
  console.log('errors:', errors.length ? errors : 'none');
  await browser.close();
})();
