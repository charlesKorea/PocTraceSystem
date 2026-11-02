# updates - 변경 파일만 담은 업데이트 zip

전체 저장소를 다시 받지 않고, 마지막 업데이트에서 바뀐 파일만 받기 위한 폴더입니다.

| 폴더 | 내용 | 변경 파일 목록 |
|---|---|---|
| `2026-11-02_web-voc/` | Web.exe VOC 11건 반영 | [CHANGED-FILES.md](2026-11-02_web-voc/CHANGED-FILES.md) |

## 사용법

1. 가장 최근 폴더의 `CHANGED-FILES.md`에서 바뀐 파일, 구분(소스/wwwroot/문서/도구), 마지막 커밋 시각을 확인한다.
2. 같은 폴더의 `PocTrace-update-<이름>.zip`만 받아 저장소 루트 기준으로 덮어쓴다.
3. `소스` 구분 파일이 있으면 해당 프로젝트를 다시 빌드해 공유 폴더에 배포한다.

## 새 업데이트 zip 만들기

```powershell
.\tools\Make-UpdateZip.ps1 -From <이전 커밋> -Name <yyyy-MM-dd_이름>
```

`updates\<이름>\`에 zip과 `CHANGED-FILES.md`가 생성된다. 커밋해서 push하면 된다.
