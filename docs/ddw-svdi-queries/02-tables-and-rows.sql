-- 02 모든 테이블 이름과 대략 행수. 빠름.
-- sys.partitions 의 행수라 정확한 COUNT 가 아니다.
-- 조인은 원격끼리만 한다. 결과 열은 table_name, row_count. 표를 통째로 복사하거나 사진을 찍는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    t.name AS table_name,
    SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS row_count
FROM remote_monami.monami.sys.tables AS t
JOIN remote_monami.monami.sys.partitions AS p
    ON p.object_id = t.object_id
GROUP BY t.name
ORDER BY t.name;
