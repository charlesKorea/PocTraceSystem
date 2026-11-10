-- 05 이름에 tag, construction, element, hook, connector, bim, subdivision, s5 가 들어간 열. 빠름.
-- WebDdw 인지 TbStar 인지(D-01)를 이 결과로 닫는다.
-- 열 이름만. 값은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS
WHERE
    LOWER(COLUMN_NAME) LIKE '%tag%'
    OR LOWER(COLUMN_NAME) LIKE '%construction%'
    OR LOWER(COLUMN_NAME) LIKE '%element%'
    OR LOWER(COLUMN_NAME) LIKE '%hook%'
    OR LOWER(COLUMN_NAME) LIKE '%connector%'
    OR LOWER(COLUMN_NAME) LIKE '%bim%'
    OR LOWER(COLUMN_NAME) LIKE '%subdivision%'
    OR LOWER(COLUMN_NAME) LIKE '%s5%'
    OR LOWER(COLUMN_NAME) LIKE '%eqcode%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
