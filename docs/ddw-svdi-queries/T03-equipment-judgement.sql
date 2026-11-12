-- T03 설비 판정 근거. 보통. 템플릿 148,110 행, 모델 약 102만 행.
-- model_type 50594049 만으로 설비로 보는지, family_key = equipment 와 bim_yn 이 어떻게 겹치는지 본다.
-- 값은 숫자와 family_key 앞 24자만. 모두 원격끼리 조인한다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- T03-A 템플릿의 model_type x family_key x bim_yn 건수 상위 40
SELECT TOP 40
    model_type,
    LEFT(family_key, 24) AS family24,
    bim_yn,
    COUNT(*) AS templates
FROM remote_monami.monami.dbo.tb_modeltemplates WITH (NOLOCK)
WHERE version_sequence < 1000000000
GROUP BY model_type, LEFT(family_key, 24), bim_yn
ORDER BY COUNT(*) DESC;

-- T03-B 인스턴스 model_type 50594049, 50594048, 51380485 의 템플릿 구성
SELECT
    mi.model_type,
    LEFT(mt.family_key, 24) AS family24,
    mt.bim_yn,
    COUNT(*) AS instances
FROM remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
LEFT JOIN remote_monami.monami.dbo.tb_modeltemplates AS mt WITH (NOLOCK)
    ON mt.model_template_id = mi.model_template_id
   AND mt.version_sequence < 1000000000
WHERE mi.version_sequence < 1000000000
  AND mi.model_type IN (50594049, 50594048, 51380485)
GROUP BY mi.model_type, LEFT(mt.family_key, 24), mt.bim_yn
ORDER BY mi.model_type, COUNT(*) DESC;

-- T03-C tag 속성을 가진 인스턴스가 어떤 model_type, family_key 인지. 설비가 아닌 것에도 tag 가 붙는지 본다.
SELECT TOP 40
    mi.model_type,
    LEFT(mt.family_key, 24) AS family24,
    mt.bim_yn,
    COUNT(*) AS tagged_instances
FROM remote_monami.monami.dbo.tb_attributes AS a WITH (NOLOCK)
JOIN remote_monami.monami.dbo.tb_modelinstances AS mi WITH (NOLOCK)
    ON mi.instance_id = a.owner_id
   AND mi.version_sequence < 1000000000
LEFT JOIN remote_monami.monami.dbo.tb_modeltemplates AS mt WITH (NOLOCK)
    ON mt.model_template_id = mi.model_template_id
   AND mt.version_sequence < 1000000000
WHERE a.attribute_nm = N'tag'
  AND a.version_sequence < 1000000000
GROUP BY mi.model_type, LEFT(mt.family_key, 24), mt.bim_yn
ORDER BY COUNT(*) DESC;
