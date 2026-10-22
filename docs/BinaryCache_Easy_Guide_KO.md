# 공유 캐시에는 무엇이 들어 있나요?（비개발자용）

> PocTrace BinaryCache 쉬운 안내 · 동료 공유용  
> 기본 위치: `X:\jHOOK\PocTraceData` · 라인별: x1, x2, x2_2 …

---

## 한 줄 요약

Admin이 매일(또는 필요할 때) BIM/배관 DB에서 뽑아 둔 **오프라인 스냅샷**입니다.

배치된 설비의 **Tag·이름·건설코드**, **연결점(POC)**, **Nozzle / Stick / Takeoff(댐퍼)**, **배관(길이·연결)**, **Hookup 번호**, **BIM Group 경로**까지 들어 있고, 이것으로 **배관을 따라가는 추적(Trace)** 과 각종 검수·엑셀 자동 생성이 가능합니다.

Viewer·철거검수(Demolition)·앞으로의 자동화는 이 폴더만 읽으면 됩니다. 인터넷·CDN 없이 공유 드라이브에서 동작합니다.  
캐시가 너무 오래되면(기본 약 **48시간**) Viewer는 갱신을 요구합니다.

---

## 폴더를 식당 메뉴판처럼 보면

| 폴더 | 쉬운 비유 | 안에 있는 것 |
|------|-----------|--------------|
| `GraphCache_라인` | 본메뉴 (메인 요리) | 설비 · 연결점 · 배관 · Takeoff · Hookup — Trace·검수의 핵심 |
| `BimGroupCache_라인` | 사이드 (그룹/경로) | Revit/BIM 그룹 트리 → EQPID가 어느 경로·그룹에 속하는지 |
| `TemplateEqpCache_라인` | 별도 메뉴 (3-% 템플릿) | 일반 Trace에 안 넣는 템플릿 설비 — 철거 「설비정보」 보강용 |

---

## GraphCache 안 파일 — 업무 말로

### nodes.bin ≈ 배치된 모든 설비·피팅 목록

현장 모델의 설비·부품 **Tag(EQPID), 이름, 구분, 공정(Sub), 건설코드, BIM ID** 등이 있습니다.  
「이 Tag가 캐시에 있나?」「건설코드가 뭔가?」를 바로 조회할 수 있습니다.  
(철거 Delete 등은 저장 단계에서 걸러진 상태입니다.)

### pocs.bin ≈ POC / Connector (설비에 달린 배관 입·출구)

각 설비의 연결점입니다. **Connector Tag, Connection Tag, Utility, 구경, nozzle/stick 구분** 등이 있고, 「어느 설비에 달린 구멍인지」로 이어져 있습니다.  
Nozzle·Stick 시트·연결 검수가 여기서 나옵니다.

### pipes.bin ≈ 배관 구간

파이프 한 구간씩의 **길이, Utility, 양끝 연결, 작업일·작업자**가 있습니다.  
Trace가 「이 구멍에서 저 구멍으로」 따라갈 때 실제로 걷는 길이 이 파일입니다. 설계 진행률(길이 합)에도 쓰입니다.

### takeoffs.bin ≈ Takeoff / Damper

가지치기·댐퍼 지점입니다. **Tag(예: Damper에 적힌 EQPID)와 연결된 배관**이 있어, TakeOff 시트·특수 Trace 계속 추적에 사용합니다.

### dynprops.bin ≈ Hookup 번호 · POC 번호

객체에 붙어 있는 동적 속성입니다. 특히 **Hookup Number**에 EQPID가 들어 있으면, 배관을 일일이 따라가지 않고도 속성만 훑어 찾을 수 있습니다. (철거 「Hook Up Number」 시트)

### manifest.bin ≈ 유통기한 라벨

언제 저장했는지, 대략 몇 건인지. Viewer는 너무 오래된 캐시면 시작을 막고 Admin으로 다시 만들도록 안내합니다.

---

## 사이드 폴더

| 폴더 | 쉬운 설명 | 어디에 쓰이나 |
|------|-----------|----------------|
| `BimGroupCache` | BIM/그룹 이름 트리 (부모→자식) | 철거 Group 시트: EQPID가 속한 경로 |
| `TemplateEqpCache` | 3-% 템플릿 설비만 따로 | 철거 설비정보에 템플릿 장비 보강 |

---

## 그래서 무엇을 할 수 있나

| 하고 싶은 일 | 캐시로 가능한가 | 비고 |
|--------------|-----------------|------|
| 설비 Tag·이름·건설코드 목록 | 가능 | nodes (+ 필요 시 Template) |
| Nozzle / Stick / Takeoff 찾기 | 가능 | pocs · takeoffs 스캔 |
| 배관 따라가기 (Trace) | 가능 | POC→Pipe→… 연결로 추적 |
| 연결·모델링 검수 | 가능 | Viewer / Admin 일배치 |
| Hookup Number ↔ EQPID | 가능 | dynprops 속성 스캔 |
| BIM Group 경로 | 가능 | BimGroupCache |
| 실시간 DB 직접 조회 | 보통 불필요 | 캐시가 스냅샷 역할 |

---

## 철거검수(Demolition)가 이미 보여 준 것

Input Excel의 EQPID만 있으면, 위 캐시를 읽어 **설비정보 · Status · Nozzle · Stick · TakeOff · Hookup · Group** 엑셀을 만듭니다. **DB에 다시 붙지 않습니다.**

같은 방식으로 다른 자동화(목록 추출, 품질 점검, 현황판)도 확장할 수 있습니다.

---

## 기억해 둘 점

- 캐시는 **「그 순간」의 복사본**입니다. 모델이 바뀌면 Admin으로 다시 저장해야 최신입니다.
- 라인(x1, x2…)마다 폴더가 따로 있습니다.

---

*관련: 中文版 `BinaryCache_Easy_Guide_ZH.md` · 프로젝트 docs 폴더*
