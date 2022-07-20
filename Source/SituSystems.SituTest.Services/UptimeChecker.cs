using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services
{
    public class UptimeChecker : IUptimeChecker
    {
        public UptimeChecker()
        {
        }

        public async Task Run(List<ServiceChecker> serviceCheckers)
        {
            // Cycle through all registered checkers
            foreach (var checker in serviceCheckers)
            {
                var checkSuccessful = false;
                try
                {
                    var currentAttempt = 1;
                    bool ExceededMaxAttempts() => currentAttempt > checker.MaxRetryAttempts;
                    while (!ExceededMaxAttempts() && !checkSuccessful)
                    {
                        var messageTemplate = $"Running {{CheckerName}}, attempt {currentAttempt} of {checker.MaxRetryAttempts}";
                        Log.Information(messageTemplate, checker.Name);
                        checkSuccessful = checker.RunCheckAndGetResult();
                        if (!checkSuccessful)
                        {
                            Log.Information("{CheckerName} check failed", checker.Name);
                        }

                        currentAttempt++;
                    }

                    if (ExceededMaxAttempts())
                    {
                        checker.LogErrors();
                    }
                }
                catch (Exception ex)
                {
                    checker.AddError("Exception encountered while running check", ex);
                    checker.LogErrors();
                }
            }
        }
    }
}