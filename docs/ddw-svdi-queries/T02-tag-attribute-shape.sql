-- T02 Tag 속성의 모양. 보통. 속성 테이블 약 1,195만 행에서 attribute_nm = 'tag' 만 원격에서 거른다.
-- Tag 값은 내지 않는다. 건수와 길이만.
-- 10분이 넘으면 TIMEOUT 과 블록 이름을 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- T02-A owner_type 별 live 건수, 빈 값, 값 길이
SELECT
    owner_type,
    COUNT(*) AS rows_live,
    SUM(CASE WHEN attribute_val IS NULL OR LTRIM(RTRIM(attribute_val)) = '' THEN 1 ELSE 0 END) AS blank_val,
    MIN(LEN(attribute_val)) AS min_len,
    MAX(LEN(attribute_val)) AS max_len
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE attribute_nm = N'tag'
  AND version_sequence < 1000000000
GROUP BY owner_type
ORDER BY owner_type;

-- T02-B 삭제된 tag 속성 행수(version_sequence 1e9 이상)
SELECT COUNT(*) AS rows_deleted
FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
WHERE attribute_nm = N'tag'
  AND version_sequence >= 1000000000;

-- T02-C 같은 Tag 값이 여러 owner 에 붙은 정도. 값은 내지 않는다.
SELECT
    COUNT(*) AS tag_values,
    SUM(CASE WHEN n > 1 THEN 1 ELSE 0 END) AS values_with_many_owners,
    MAX(n) AS max_owners_per_value
FROM (
    SELECT attribute_val, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
    WHERE attribute_nm = N'tag'
      AND version_sequence < 1000000000
    GROUP BY attribute_val
) AS s;

-- T02-D 한 owner 에 live tag 행이 둘 이상인 정도
SELECT
    COUNT(*) AS owners,
    SUM(CASE WHEN n > 1 THEN 1 ELSE 0 END) AS owners_with_many_tag_rows,
    MAX(n) AS max_rows_per_owner
FROM (
    SELECT owner_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_attributes WITH (NOLOCK)
    WHERE attribute_nm = N'tag'
      AND version_sequence < 1000000000
    GROUP BY owner_id
) AS s;
