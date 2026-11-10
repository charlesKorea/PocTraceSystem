-- 06 버전, Hookup, CMM, 속성처럼 보이는 테이블과 뷰. 빠름.
-- 이름만. 행 내용은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.name AS object_name,
    o.type_desc
FROM remote_monami.monami.sys.objects AS o
WHERE o.is_ms_shipped = 0
  AND (
        LOWER(o.name) LIKE '%version%'
        OR LOWER(o.name) LIKE '%hook%'
        OR LOWER(o.name) LIKE '%cmm%'
        OR LOWER(o.name) LIKE '%attribute%'
        OR LOWER(o.name) LIKE '%connector%'
        OR LOWER(o.name) LIKE '%dynamic%'
      )
ORDER BY o.type_desc, o.name;
