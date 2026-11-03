# 업데이트 2026-11 Report / Progress / Web 배치 ZIP

## 1. Web.exe 배치 검수 (UI.exe와 동일한 결과물)

- 배치(태그 N개)는 원래도 **개별 파일 N개 + Summary 1개**를 `%LOCALAPPDATA%\PocTraceWeb\jobs\<id>\`에 만들었다.
  다만 웹 화면에는 Summary만 내려받기 링크가 있어 개별 파일은 받을 수 없었다.
- 이번 업데이트: 작업 폴더의 xlsx 전체를 ZIP으로 묶어 `/api/jobs/{id}/zip`으로 제공하고, 결과 영역에 **"ZIP으로 받기"** 링크를 추가했다.
- 태그에 결과가 없으면 파일을 만들지 않고 경고로 남긴다. 같은 이름의 파일은 `_2`, `_3`으로 구분한다.

## 2. Admin 일일 리포트(HTML)

| 화면 | 변경 |
|---|---|
| `index.html` 라인별 품질 추세 비교 | 일/주/월 버튼 추가, % 소수점 없음 |
| `report_x1.html` 일별 추세 분석 | POC Pass / H/U Pass 2개만, 정수 숫자 라벨. 수량 계열 삭제. 주간(ISO 주) 집계 오류 수정 |
| 오류 유형 분석 파이 | 범례와 파이 사이 간격 확보 (원인 귀속, H/U 파이도 동일) |
| 공정별 검수 현황 | POC 기준 검수 설비 수 / Fail 설비 수 / Fail Rate만 표시. 다크 모드에서 Fail Rate는 노란색 배지로 강조 |
| 상세보기 > Detail 클릭 | 한글 깨짐 수정 (app.js의 낡은 중복 코드 제거, 누락된 언어 키 추가) |
| `progress.html` | 한 페이지 10행, 최근 날짜순. 깨진 헤더 정렬과 `progress_low_conf_short` 키 누락 수정 |

H/U Pass는 `Stats_History`에 `HookupPassRate`/`HookupTotal`이 저장되면서부터 정확한 값이 표시된다.
이전 History 행은 `H/U = AvgPassRate*2 - POC`로 역산한다.

## 3. 설계 진척 "최근 날짜"

- 설비에 속한 **배관(Pipe)** 만 대상으로, S5D `PipeLog`에서 **Length가 바뀐 행**만 변경으로 본다(허용오차 0.05mm, 최초 기록은 변경으로 인정).
- 최근 날짜 = 그 설비 배관들 중 가장 마지막 Length 변경일, 최초 날짜 = 가장 이른 기록일.
- PipeLog를 읽을 수 없으면(미연결 등) 기존처럼 `Pipe.OpTime`으로 대체한다.
- `progress_btn_utility` 버튼은 저장소 어디에도 없다(이전 라운드에서 이미 삭제, Tag 클릭으로 유틸리티 펼침). 삭제할 대상이 더 없다.

## 4. 배포

1. `PocTrace.Core`를 Release로 빌드한다. Templates(js/css)와 `PocTrace.Core.dll`이 함께 나온다.
2. `PocTrace.Admin`을 빌드해 `D:\PocAdmin`에 배포한다. Templates 폴더도 덮어쓴다.
3. `PocTrace.Web`을 빌드해 공유 폴더의 `PocTrace.Web.exe`를 바꾸고, `wwwroot\index.html`을 덮어쓴다.
4. 공유 폴더의 UI/철거/노즐 exe는 새 `PocTrace.Core.dll` 하나를 보도록 같은 빌드 산출물로 맞춘다 (exe와 빌드를 섞지 않는다).
5. Admin에서 Full을 한 번 실행해 `data_*.js`와 `Stats_History`를 다시 만든다. 이후 추세 그래프의 H/U가 정확해진다.
6. 폴더에 남은 Serilog/EPPlus/Dapper/JLic.Core.dll은 지운다 (Costura 내장).
