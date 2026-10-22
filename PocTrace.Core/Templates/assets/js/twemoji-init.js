/* Offline Twemoji bootstrap ??include AFTER emoji/twemoji.min.js
   Usage in layout.html (after body content):
     <script src="assets/emoji/twemoji.min.js"></script>
     <script src="assets/js/twemoji-init.js"></script>
   Note: default Twemoji CDN for images needs network OR set base to local SVG folder.
   For true airgap, download Twemoji SVG zip and set:
     twemoji.base = 'assets/emoji/svg/';
*/
(function () {
  function run() {
    if (typeof twemoji === 'undefined') return;
    twemoji.parse(document.body, {
      folder: 'svg',
      ext: '.svg',
      // Airgap: local SVG folder under web-assets/emoji/svg (downloaded by this script).
      base: 'assets/emoji/'
    });
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
