-- SLOW-02 배관 PIPE_TYPE 분포. 배관 테이블 전체 집계.
-- 17301768 이 Takeoff 로 몇 건인지, 그 외 유형이 몇 개인지 본다.
-- 결과는 유형 수만큼이라 짧다. 10분이 넘으면 TIMEOUT.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    pipe_type,
    SUM(CASE WHEN operationtype < 2 THEN 1 ELSE 0 END) AS live_rows,
    SUM(CASE WHEN operationtype >= 2 THEN 1 ELSE 0 END) AS not_live,
    SUM(CASE WHEN operationtype < 2 AND (length1 IS NULL OR length1 <= 0) THEN 1 ELSE 0 END) AS live_length_empty
FROM remote_monami.monami.dbo.tb_pipeinstances WITH (NOLOCK)
GROUP BY pipe_type
ORDER BY pipe_type;
