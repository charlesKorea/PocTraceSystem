-- 03 캐시에 쓰는 테이블의 열 명세. 빠름.
-- 원격 메타만 로컬 #cols 로 한 번 가져온다. 큰 테이블과 조인하지 않는다.
-- 결과 열 line 만 복사한다. 테이블당 한 줄이다.
-- 사용자 테이블에 LOGIN_PW 같은 열 이름이 보여도 값은 읽지 않는다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#cols') IS NOT NULL DROP TABLE #cols;

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    ORDINAL_POSITION,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
INTO #cols
FROM remote_monami.monami.INFORMATION_SCHEMA.COLUMNS;

SELECT
    c.TABLE_NAME + ' :: ' + STUFF((
        SELECT
            ' | ' + c2.COLUMN_NAME + ' ' + c2.DATA_TYPE
            + CASE
                WHEN c2.CHARACTER_MAXIMUM_LENGTH IS NULL THEN ''
                WHEN c2.CHARACTER_MAXIMUM_LENGTH < 0 THEN '(max)'
                ELSE '(' + CONVERT(varchar(10), c2.CHARACTER_MAXIMUM_LENGTH) + ')'
              END
            + CASE WHEN c2.IS_NULLABLE = 'NO' THEN ' NN' ELSE '' END
        FROM #cols AS c2
        WHERE c2.TABLE_NAME = c.TABLE_NAME
        ORDER BY c2.ORDINAL_POSITION
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 3, '') AS line
FROM #cols AS c
WHERE LOWER(c.TABLE_NAME) IN (
    'tb_modelinstances',
    'tb_modeltemplates',
    'tb_division',
    'tb_pocinstances',
    'tb_poctemplates',
    'tb_pipesize',
    'tb_pipeinstances',
    'tb_utilities',
    'tb_utility_groups',
    'tb_attributes',
    'tb_instancegroups',
    'tb_instancegroupmembers',
    'tb_new_users',
    'tb_new_usergroups',
    'tb_versions'
)
GROUP BY c.TABLE_NAME
ORDER BY c.TABLE_NAME;
