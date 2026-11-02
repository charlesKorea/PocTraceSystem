# Web 표가 머리글을 벗어나는 문제와 표 작성 규칙

대상: `PocTrace.Web/wwwroot`의 모든 표 (`index.html`, `history.html`, 앞으로 추가하는 화면).
관련 CSS: `wwwroot/assets/jcs-ui/jcs-glass.css`

## 어떤 문제였나

철거검수에서 Excel을 올리면 검증 표가 나오는데, 아래로 스크롤하면 **본문 행이 머리글 위로 겹쳐 보이고**, 열 폭이 좁아 글자가 한 글자씩 줄바꿈되었다 (`라 인`, `PROCESS-` / `3`).

브라우저에서 재현한 결과 (40행, 긴 건설코드와 사유):

| 증상 | 원인 | 고친 방법 |
|------|------|-----------|
| 스크롤하면 본문 글자가 머리글 뒤로 비쳐 보임 | `th`의 배경이 반투명(`--chip`)이라 `position: sticky`인 머리글 뒤가 비침 | `th`에 불투명 바탕(`--card-solid`)을 깔고 그 위에 `--chip`을 `linear-gradient`로 덮음 |
| 열 폭이 좁아 한 글자씩 줄바꿈 | 표 `width:100%`만 주어 긴 값이 있는 열이 다른 열을 눌러 버림 | 표 `min-width: max-content`, 셀 `white-space: nowrap`. 긴 문장 열만 `cell-wrap`으로 줄바꿈 허용 |
| 표가 카드 밖으로 밀려남 | `grid`의 칸은 `min-width: auto`라 안의 표 폭만큼 늘어남 | `.grid-2 > * { min-width: 0 }`, `.card { min-width: 0 }`, `grid-template-columns: minmax(0, 1fr)` |
| 머리글 칸 수와 본문 칸 수가 다름 | 행마다 셀 수가 다른 데이터를 그대로 그림 | 그리기 전에 최대 열 수를 구해 머리글과 모든 행을 같은 수로 맞춤 (`paintDemoResult`) |
| 클래스 이름 충돌 | 셀에 `class="wrap"`을 썼더니 `.wrap`(페이지 폭, `padding: 18px 20px 56px`)이 같이 적용되어 행 높이가 커짐 | 이름을 `cell-wrap`으로 바꿈. **일반 단어 클래스를 표 셀에 쓰지 않는다** |

## 표를 만들 때 규칙

1. 표는 반드시 `div.table-wrap` 안에 둔다. 세로 스크롤과 가로 스크롤은 이 상자가 맡고, 페이지 전체가 가로로 밀리면 안 된다.

   ```html
   <div class="table-wrap">
     <table>
       <thead><tr><th>EQPID</th><th class="num">건수</th></tr></thead>
       <tbody>...</tbody>
     </table>
   </div>
   ```

2. 머리글은 `thead th`에 둔다. 스타일은 CSS가 준다 (고정, 불투명, 줄바꿈 없음). 인라인 `background`를 주지 않는다.
3. 숫자 열은 `th`, `td` 모두 `class="num"` (오른쪽 정렬, 같은 폭 숫자). 인라인 `style="text-align:right"`를 쓰지 않는다.
4. 긴 문장이 들어가는 열(사유, 메시지)만 `td.cell-wrap`을 쓴다 (`min-width 14em`, `max-width 34em`). 나머지 셀은 줄바꿈하지 않는다.
5. 머리글 칸 수 = 모든 본문 행의 칸 수. JS에서 표를 만들 때는 최대 열 수를 먼저 구하고 부족한 칸은 빈 문자열로 채운다. 안내 한 줄짜리 행은 `colspan`을 머리글 칸 수와 같게 쓴다.
6. 표 옆의 `grid` 안에 표를 넣어야 하면 그 칸에 `min-width: 0`이 있는지 확인한다 (`.grid-2`는 이미 적용됨).
7. 새 표 클래스나 셀 클래스를 만들 때 `.wrap`, `.card`, `.hint`, `.bar`, `.status`처럼 이미 CSS에 있는 이름을 쓰지 않는다. 먼저 `jcs-glass.css`에서 검색한다.
8. 표 높이는 `.table-wrap`의 `max-height`(기본 440px, 검수 기록은 62vh)로 제한한다. 행 수가 500행을 넘을 수 있으면 서버에서 잘라 보내고(`MaxSummaryRows`) 화면에 안내를 둔다.

## 확인 방법

화면을 바꾼 뒤 아래 두 가지를 눈으로 본다.

- 행이 40개 이상인 데이터로 표를 아래로 스크롤했을 때 본문 글자가 머리글에 비치지 않는다.
- 브라우저 창 폭을 1000px 정도로 줄였을 때 표가 카드 밖으로 나가지 않고, 표 상자 안에서 가로 스크롤된다.

개발자 도구 콘솔에서 머리글과 첫 행의 열 위치가 같은지 볼 수 있다.

```js
var th = [...document.querySelectorAll('.table-wrap thead th')].map(e => Math.round(e.getBoundingClientRect().left));
var td = [...document.querySelectorAll('.table-wrap tbody tr:first-child td')].map(e => Math.round(e.getBoundingClientRect().left));
JSON.stringify(th) === JSON.stringify(td)
```
