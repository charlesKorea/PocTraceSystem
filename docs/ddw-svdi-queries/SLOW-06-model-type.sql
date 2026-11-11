-- SLOW-06 모델 유형과 Composite, 부모. 모델 테이블 집계.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- MODEL_TYPE 단독으로 설비를 만들지 않는다. 이 숫자는 후보 필터를 확인할 때 쓴다.
-- 50594049, 50594048 이 보이면 그 행의 composite, 부모 비율을 같이 본다.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    model_type,
    COUNT(*) AS rows_all,
    SUM(CASE WHEN composite_yn <> 0 THEN 1 ELSE 0 END) AS composite_on,
    SUM(CASE
            WHEN parent_instance_id IS NULL
              OR parent_instance_id = '00000000-0000-0000-0000-000000000000' THEN 1
            ELSE 0
        END) AS parent_null
FROM remote_monami.monami.dbo.tb_modelinstances WITH (NOLOCK)
GROUP BY model_type
ORDER BY COUNT(*) DESC;
