-- 01B 삭제(live) 판정 열 확인. 빠름. 선택 사항이다. 2026-11-11 담당자가 모든 tb_* 에 version_sequence 가 있고
-- operationtype 이 없다고 확인했다. 그 확인을 쿼리 결과로 남기고 싶을 때만 돌린다.
-- 2026-11-11 SVDI 결과: tb_instancegroups 에는 operationtype 이 없다. 다른 tb_* 에도 없는지,
-- version_sequence 가 어느 테이블에 있는지 열 이름만 본다. 값은 읽지 않는다.
-- 결과 그리드를 모두 복사한다. 행은 tb_ 테이블 수만큼이다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    TABLE_NAME,
    MAX(CASE WHEN LOWER(COLUMN_NAME) = 'operationtype' THEN 1 ELSE 0 END) AS has_operationtype,
    MAX(CASE WHEN LOWER(COLUMN_NAME) = 'version_sequence' THEN 1 ELSE 0 END) AS has_version_sequence
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS
WHERE LOWER(TABLE_NAME) LIKE 'tb[_]%'
GROUP BY TABLE_NAME
ORDER BY TABLE_NAME;
