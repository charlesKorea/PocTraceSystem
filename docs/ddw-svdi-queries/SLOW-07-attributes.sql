-- SLOW-07 속성 이름 존재. 가장 느릴 수 있다. 마지막에 한 번만.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- TB_ATTRIBUTES 는 11,953,709행이다(02 결과, 2026-11-11). 초안의 1억 행 가정보다 작다. 필요한 이름만 IN 으로 건다.
-- 이름 목록 전체를 집계하지 않는다. 로컬 임시 테이블과 조인하지 않는다.
-- 값은 앞 24자와 길이만. Tag 로 보이는 긴 문자열을 더 복사하지 않는다.
-- 30분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    attribute_nm,
    owner_type,
    COUNT(*) AS rows_all,
    SUM(CASE WHEN attribute_val IS NULL OR LTRIM(RTRIM(attribute_val)) = '' THEN 1 ELSE 0 END) AS blank_val,
    MIN(LEN(attribute_val)) AS min_len,
    MAX(LEN(attribute_val)) AS max_len
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE attribute_nm IN (
        N'Element ID_Number',
        N'Element ID',
        N'__MODULE_INFO',
        N'ConnectionTag',
        N'CONNECTIONTAG',
        N'BIM_DESIGN_INFO',
        N'S5_LATERAL NUMBER',
        N'ConvertDate',
        N'HookNumber',
        N'HOOKUPNUMBER',
        N'Hookup Number',
        N'HookUp Number',
        N'ConnectorUse',
        N'CONNECTORUSE',
        N'POCNO',
        N'PocNo',
        N'ConstructionCode',
        N'CONSTRUCTIONCODE',
        N'construction_code'
      )
GROUP BY attribute_nm, owner_type
ORDER BY attribute_nm, owner_type;

SELECT TOP 5
    attribute_nm,
    owner_type,
    LEN(attribute_val) AS val_len,
    LEFT(attribute_val, 24) AS val24
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE attribute_nm = N'__MODULE_INFO';
