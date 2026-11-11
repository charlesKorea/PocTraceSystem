-- SLOW-02 배관 PIPE_TYPE 분포. 배관 테이블 전체 집계.
-- rev2 2026-11-11: operationtype 열이 없는 DDW라 live 필터 없이 삭제 행을 포함한 전체 행을 본다. live는 version_sequence 임계 확정(01B, SLOW-01) 뒤에 건다.
-- 17301768 이 Takeoff 로 몇 건인지, 그 외 유형이 몇 개인지 본다.
-- 결과는 유형 수만큼이라 짧다. 10분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    pipe_type,
    COUNT(*) AS rows_all,
    SUM(CASE WHEN length1 IS NULL OR length1 <= 0 THEN 1 ELSE 0 END) AS length_empty
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
GROUP BY pipe_type
ORDER BY pipe_type;
