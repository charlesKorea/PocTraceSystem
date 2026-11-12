-- T04 POC 의 모양과 연결 대칭성. 보통에서 느림. POC 약 147만 행.
-- poc_template_id 가 0 인 POC 가 배관 끝점(owner_type 256)과 같은 집합인지,
-- connector_pipe(model_type 51380485)가 POC 를 몇 개 갖는지, 연결이 서로를 가리키는지 본다.
-- T04-D 는 자기 조인이라 가장 길다. 10분이 넘으면 T04-D 만 취소하고 TIMEOUT 이라고 보낸다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- T04-A owner_type x 템플릿 0 여부 x 연결 상태
SELECT
    owner_type,
    CASE WHEN poc_template_id IS NULL THEN 'null'
         WHEN poc_template_id = 0x00000000000000000000000000000000 THEN 'zero'
         ELSE 'set' END AS poc_template,
    CASE WHEN connected_poc_id IS NULL THEN 'null'
         WHEN connected_poc_id = 0x00000000000000000000000000000000 THEN 'zero'
         ELSE 'set' END AS connected,
    COUNT(*) AS n
FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
WHERE version_sequence < 1000000000
GROUP BY owner_type,
    CASE WHEN poc_template_id IS NULL THEN 'null'
         WHEN poc_template_id = 0x00000000000000000000000000000000 THEN 'zero'
         ELSE 'set' END,
    CASE WHEN connected_poc_id IS NULL THEN 'null'
         WHEN connected_poc_id = 0x00000000000000000000000000000000 THEN 'zero'
         ELSE 'set' END
ORDER BY owner_type, poc_template, connected;

-- T04-B connector_pipe 하나가 가진 POC 개수 분포
SELECT
    n AS poc_per_owner,
    COUNT(*) AS owners
FROM (
    SELECT p.owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
    JOIN remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
        ON mi.instance_id = p.owner_instance_id
       AND mi.version_sequence < 1000000000
       AND mi.model_type = 51380485
    WHERE p.version_sequence < 1000000000
    GROUP BY p.owner_instance_id
) AS s
GROUP BY n
ORDER BY n;

-- T04-C 배관 하나가 가진 POC(템플릿 0) 개수 분포. 6개까지와 90개 이상만.
SELECT
    n AS poc_per_pipe,
    COUNT(*) AS pipes
FROM (
    SELECT owner_instance_id, COUNT(*) AS n
    FROM remote_monami.monami.dbo.tb_pocinstances WITH (NOLOCK)
    WHERE version_sequence < 1000000000
      AND poc_template_id = 0x00000000000000000000000000000000
    GROUP BY owner_instance_id
) AS s
WHERE n <= 6 OR n >= 90
GROUP BY n
ORDER BY n;

-- T04-D 연결 대칭성. 상대 POC 가 나를 가리키지 않는 건수와, 상대가 live 가 아닌 건수.
SELECT
    SUM(CASE WHEN t.instance_id IS NULL THEN 1 ELSE 0 END) AS target_missing_or_deleted,
    SUM(CASE WHEN t.instance_id IS NOT NULL
              AND (t.connected_poc_id IS NULL OR t.connected_poc_id <> p.instance_id) THEN 1 ELSE 0 END) AS not_reciprocal,
    COUNT(*) AS connected_pocs
FROM remote_monami.monami.dbo.tb_pocinstances AS p WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_pocinstances AS t WITH (NOLOCK)
    ON t.instance_id = p.connected_poc_id
   AND t.version_sequence < 1000000000
WHERE p.version_sequence < 1000000000
  AND p.connected_poc_id IS NOT NULL
  AND p.connected_poc_id <> 0x00000000000000000000000000000000;
