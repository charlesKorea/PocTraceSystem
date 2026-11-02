/* 이 화면이 열려 있음을 Web.exe 에 알린다. 모든 화면이 닫히면 Web.exe 는 자동 종료한다.
   서버가 사라지면 화면 위에 안내를 띄운다. */
(function () {
  var id = Math.random().toString(36).slice(2) + Date.now().toString(36);
  var fails = 0;
  var banner = null;
  var TEXT = {
    ko: "PocTrace.Web 프로그램이 종료되었습니다. Web.exe 를 다시 실행한 뒤 이 화면을 새로고침하세요.",
    zh: "PocTrace.Web 程序已退出。请重新运行 Web.exe 后刷新此页面。"
  };

  function lang() {
    return localStorage.getItem("poc_lang") === "zh" ? "zh" : "ko";
  }
  function showGone() {
    if (!banner) {
      banner = document.createElement("div");
      banner.className = "server-gone";
      banner.setAttribute("role", "alert");
      document.body.appendChild(banner);
    }
    banner.textContent = TEXT[lang()];
    banner.hidden = false;
  }
  function hideGone() {
    if (banner) banner.hidden = true;
  }
  function beat() {
    fetch("/api/heartbeat?id=" + encodeURIComponent(id), { method: "POST", keepalive: true })
      .then(function (res) {
        if (!res.ok) throw new Error("bad");
        fails = 0;
        hideGone();
      })
      .catch(function () {
        fails++;
        if (fails >= 3) showGone();
      });
  }
  function bye() {
    try {
      navigator.sendBeacon("/api/bye?id=" + encodeURIComponent(id));
    } catch (err) { /* 닫히는 중이라 실패해도 서버가 시간 초과로 정리한다 */ }
  }

  window.addEventListener("pagehide", bye);
  window.addEventListener("pageshow", beat);
  document.addEventListener("visibilitychange", function () {
    if (!document.hidden) beat();
  });
  setInterval(beat, 5000);
  beat();
})();
