Admin HTML의 큰 정적 파일은 git에 없습니다. 화면 소스(layout.html, app.js, poc-*.js, lang.js, style.css)만 저장소에 있습니다.

빠진 것: assets/emoji, assets/fonts, *.min.js, vfs_fonts.js, *.min.css, bootstrap-icons.css

압축을 PocTrace.Core 폴더에 풀면 Templates\assets 가 채워집니다. 압축 안 첫 폴더는 Templates 입니다.

  Expand-Archive -Force .\PocTrace-Admin-Templates.zip -DestinationPath .

GitHub Release 자산 이름: PocTrace-Admin-Templates.zip
로컬에서 방금 만든 파일: dist\PocTrace-Admin-Templates.zip
