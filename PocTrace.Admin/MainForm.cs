using System;

using System.Threading.Tasks;

using System.Windows.Forms;

using PocTrace.Admin.Jobs;

using PocTrace.Core.Infrastructure;

using Quartz;

using Quartz.Impl;

using Quartz.Logging;



namespace PocTrace.Admin

{

    public partial class MainForm : Form

    {

        private IScheduler _scheduler;

        private bool _jobRunning;



        public MainForm()

        {

            InitializeComponent();

            try
            {
                this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // exe 아이콘 추출 실패 시 기본 WinForms 아이콘 유지
            }

            AdminJobProgress.Reported += OnJobProgress;

            InitScheduler();

        }



        protected override void OnFormClosed(FormClosedEventArgs e)

        {

            AdminJobProgress.Reported -= OnJobProgress;

            base.OnFormClosed(e);

        }



        private async void InitScheduler()

        {

            try

            {

                // Quartz 폴링 DEBUG 노이즈 제거 (예: "Batch acquisition of 0 triggers")
                LogProvider.IsDisabled = true;

                StdSchedulerFactory factory = new StdSchedulerFactory();

                _scheduler = await factory.GetScheduler().ConfigureAwait(true);

                await _scheduler.Start().ConfigureAwait(true);

                Log("Scheduler ready.");

            }

            catch (Exception ex)

            {

                Log($"Scheduler init failed: {ex.Message}");

            }

        }



        private async void BtnStart_Click(object sender, EventArgs e)

        {

            if (_scheduler == null) return;

            btnStart.Enabled = false;

            try

            {

                var jobKey = new JobKey("DailyJob", "Group1");

                if (await _scheduler.CheckExists(jobKey).ConfigureAwait(true))

                {

                    Log("02:00 schedule is already registered.");

                    return;

                }



                IJobDetail job = JobBuilder.Create<DailyTraceJob>()

                    .WithIdentity(jobKey)

                    .UsingJobData(DailyTraceJob.JobDataModeKey, DailyTraceJob.ModeFull)

                    .Build();



                ITrigger trigger = TriggerBuilder.Create()

                    .WithIdentity("DailyTrigger", "Group1")

                    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(02, 00))

                    .Build();



                await _scheduler.ScheduleJob(job, trigger).ConfigureAwait(true);

                Log("Scheduler started. Next run at 02:00 AM (Full batch).");

            }

            catch (Exception ex)

            {

                btnStart.Enabled = true;

                Log($"Scheduler start failed: {ex.Message}");

            }

        }



        private async void BtnRunNow_Click(object sender, EventArgs e)

        {

            await TriggerJobAsync(DailyTraceJob.ModeFull, "ManualJob").ConfigureAwait(true);

        }



        private async void BtnCacheOnly_Click(object sender, EventArgs e)

        {

            await TriggerJobAsync(DailyTraceJob.ModeCacheOnly, "CacheOnlyJob").ConfigureAwait(true);

        }



        private async void BtnDdwCache_Click(object sender, EventArgs e)

        {

            await TriggerJobAsync(DailyTraceJob.ModeDdwCache, "DdwCacheJob").ConfigureAwait(true);

        }



        private async Task TriggerJobAsync(string mode, string jobKey)

        {

            if (_scheduler == null)

            {

                Log("Scheduler not initialized.");

                return;

            }

            if (_jobRunning || DailyTraceJob.IsRunning)

            {

                Log("Job already running — wait for completion.");

                return;

            }



            SetJobRunning(true);

            Log(mode == DailyTraceJob.ModeDdwCache

                ? "DDW cache job triggered (monami → GraphCache_x2_m)..."

                : mode == DailyTraceJob.ModeCacheOnly

                ? "Cache-only job triggered (DB → BinaryCache, no trace/excel/html)..."

                : "Full batch job triggered...");



            try

            {

                IJobDetail job = JobBuilder.Create<DailyTraceJob>()

                    .WithIdentity(jobKey + DateTime.Now.Ticks, "Group1")

                    .UsingJobData(DailyTraceJob.JobDataModeKey, mode)

                    .Build();



                ITrigger trigger = TriggerBuilder.Create()

                    .StartNow()

                    .Build();



                await _scheduler.ScheduleJob(job, trigger).ConfigureAwait(true);

            }

            catch (Exception ex)

            {

                SetJobRunning(false);

                Log($"Trigger failed: {ex.Message}");

            }

        }



        private async void BtnStop_Click(object sender, EventArgs e)

        {

            if (_scheduler == null)

            {

                Log("Scheduler is not initialized.");

                return;

            }



            try

            {

                await _scheduler.Standby().ConfigureAwait(true);

                await _scheduler.Clear().ConfigureAwait(true);

                btnStart.Enabled = true;

                Log("Scheduler stopped (standby + jobs cleared).");

            }

            catch (Exception ex)

            {

                Log($"Stop failed: {ex.Message}");

            }

        }



        private void OnJobProgress(JobProgressReport report)

        {

            if (InvokeRequired)

            {

                BeginInvoke(new Action<JobProgressReport>(OnJobProgress), report);

                return;

            }



            if (!_jobRunning && report.Percent > 0 && !report.IsComplete && !report.IsError)

                SetJobRunning(true);



            string dbPart = string.IsNullOrEmpty(report.DbName) ? "" : $" [{report.DbName}]";

            lblStatus.Text = $"{report.Phase}{dbPart}" +

                (string.IsNullOrEmpty(report.Detail) ? "" : $" — {report.Detail}");



            progressBarMain.Value = Math.Max(0, Math.Min(100, report.Percent));



            if (report.StepElapsed.HasValue)

            {

                var t = report.StepElapsed.Value;

                lblStepTime.Text = $"단계 소요: {t.TotalSeconds:F1}s ({t.TotalMilliseconds:N0} ms)";

            }



            string timeSuffix = report.StepElapsed.HasValue

                ? $" ({report.StepElapsed.Value.TotalSeconds:F1}s)"

                : "";

            Log($"{report.Phase}{dbPart}: {report.Detail ?? ""}{timeSuffix} [{report.Percent}%]");



            if (report.IsComplete)

            {

                lblStatus.ForeColor = System.Drawing.Color.ForestGreen;

                if (report.StepElapsed.HasValue)

                    lblStepTime.Text = $"총 소요: {report.StepElapsed.Value.TotalMinutes:F1} min ({report.StepElapsed.Value.TotalSeconds:F0}s)";

                SetJobRunning(false);

            }

            else if (report.IsError)

            {

                lblStatus.ForeColor = System.Drawing.Color.IndianRed;

                SetJobRunning(false);

            }

            else

            {

                lblStatus.ForeColor = System.Drawing.SystemColors.ControlText;

            }

        }



        private void SetJobRunning(bool running)

        {

            _jobRunning = running;

            btnRunNow.Enabled = !running;

            btnCacheOnly.Enabled = !running;

            btnDdwCache.Enabled = !running;

            if (running)

            {

                progressBarMain.Style = ProgressBarStyle.Continuous;

            }

        }



        private void Log(string msg)

        {

            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");

        }

    }

}


