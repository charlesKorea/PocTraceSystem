# SVDI 실행 위치 (코딩할 때 이 문서를 따른다)

캐시는 하나다. exe를 두는 폴더는 두 곳이다.

| 프로그램 | 실행 위치 | Core.dll |
|---|---|---|
| Admin | `D:\PocAdmin` 에서만 실행 | 그 빌드를 D:에 둔 복사본. 캐시를 만든다 |
| UI, 철거, 노즐추적 | 같은 공유 폴더의 같은 경로 | 그 폴더의 `PocTrace.Core.dll` 하나 |
| Web | 그 공유 폴더 | 같은 `PocTrace.Core.dll`. 철거·노즐 exe를 그 자리에서 연다 |
| 삭제 배관 길이 | 그 공유 폴더에 두면 같은 Core.dll을 쓴다 | 그래프 캐시가 아니라 DB를 읽는다 |

UI, 철거, 노즐추적은 같은 캐시를 보므로 폴더를 나누지 않는다. Admin은 캐시를 만드는 쪽이라 D:에서 따로 실행한다. Admin용 `PocTrace.Core.dll`은 D:에 두고, 공유 폴더의 Core.dll과 빌드를 섞지 않는다.

`docs/SVDI_XDrive_Manual_Deploy_KO.md`의 캐시·DailyReport 경로와 Admin의 D: 실행은 현재 기준이다. 그 문서에서 폴더마다 NuGet DLL과 `JLic.Core.dll`을 두라는 목록은 예전 배포다.

## 캐시

- 캐시 폴더는 하나다. `SharedCachePath` 기본값은 `X:\jHOOK\PocTraceData`.
- 사용자별, exe별 GraphCache를 만들지 않는다.
- 캐시를 읽는 코드는 이 공유 경로만 본다. Admin만 그 경로에 쓴다.
- `PocTrace.DdwAcceptance.exe`는 공유 폴더에서 실행하지 않는다. `SharedCachePath` 폴더가 있으면 지우고 시험 캐시를 쓴다.

## 배포 DLL

공유 폴더와 `D:\PocAdmin` 모두 NuGet DLL을 늘어놓지 않는다. Costura가 Serilog, EPPlus, Dapper, SqlClient, JLic.Core를 각 exe 안에 넣는다. 한 폴더에 두는 공용 어셈블리는 **그 빌드의 `PocTrace.Core.dll` 하나**다.

| 복사하는 것 | 설명 |
|---|---|
| 각 exe와 `exe.config` | 평문 설정. 비밀번호는 비운다 |
| `PocTrace.Core.dll` | 그 솔루션 빌드 산출물. 공유 폴더에는 한 파일, D: Admin에는 그 빌드의 복사본 |
| `Lic\jlab.lic` | 라이선스 파일. 각 실행 폴더에 둔다 |
| `config\db.secret` | 그 실행 폴더의 DB 비밀번호. Admin과 공유 폴더는 파일이 따로다 |
| `wwwroot` | Web이 공유 폴더에서 읽는다 |
| Admin `Templates` | `D:\PocAdmin\Templates` |

exe와 Core.dll의 빌드가 다르면 `메서드를 찾을 수 없습니다`가 나거나 창이 뜨기 전에 끝난다. 한 솔루션 빌드에서 exe와 Core.dll을 같이 고른다.

로컬 DB(SQLite)를 쓰는 경우에만 `SQLite.Interop.dll`이 더 필요할 수 있다. 그 외 Serilog, EPPlus, Dapper, Newtonsoft, JLic.Core.dll은 폴더에 두지 않는다. 예전 배포에서 남은 그 DLL은 지운다. 폴더에 파일이 남아 있으면 exe 안에 넣은 복사본보다 그 파일이 먼저 열린다.

## DB 비밀번호

비밀번호가 들어 있는 값만 그 exe 실행 폴더의 `config\db.secret`에 암호화해서 둔다. 대상은 `SourceDb`, `PocTraceDB`, `LocalDb`, `FrmConnectionString`이다. SchemaKind, TargetDatabases, SharedCachePath 같은 나머지 설정은 exe.config 평문이다.

개인 C: 드라이브나 `C:\ProgramData`에 비밀번호를 저장하지 않는다. 암호 파일은 메모장으로 바로 안 열리게 한 것이다. exe를 가진 사람은 복호할 수 있다.

`SecretConfigDirectory`는 비운다. 값이 실행 폴더 밖이면 무시하고 그 exe 옆 `config`를 쓴다. Admin의 암호 파일은 `D:\PocAdmin\config\db.secret`이고, UI·철거·노즐·Web의 암호 파일은 공유 폴더의 `config\db.secret`이다.

exe.config에 비밀번호가 남아 있으면 실행이 `config\db.secret`에 암호화해서 넣고, 그 다음 exe.config의 비밀번호 문자열을 지운다. 암호 파일이 이미 있어도 exe.config의 비밀번호가 다르면 그 값으로 암호 파일을 갱신한 뒤 평문을 지운다. 다른 사용자가 exe.config를 열어 둔 채면 지우기가 실패할 수 있다. 그때는 다음 실행이 다시 지운다.

`C:\ProgramData\PocTrace\config\secrets.config`가 남아 있으면 암호 파일에 없는 항목만 한 번 가져온 뒤 그 ProgramData 파일은 지운다. 비밀번호를 바꿀 때는 ProgramData가 아니라 exe.config에 새 값을 넣고 한 번 실행한다.
