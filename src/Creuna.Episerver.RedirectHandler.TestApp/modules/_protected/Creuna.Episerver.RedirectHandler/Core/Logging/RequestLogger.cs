﻿using System;
using System.Collections.Generic;
using System.Linq;
using Creuna.Episerver.RedirectHandler.Core.Configuration;
using Creuna.Episerver.RedirectHandler.Core.Data;
using EPiServer.Logging;
using EPiServer.ServiceLocation;

namespace Creuna.Episerver.RedirectHandler.Core.Logging
{
    public class RequestLogger
    {
        private readonly RedirectConfiguration _redirectConfiguration;
        private static readonly RequestLogger instance = ServiceLocator.Current.GetInstance<RequestLogger>();
        private static RequestLogger instanceOverride = null;
        private static readonly ILogger Logger = LogManager.GetLogger();

        public RequestLogger(RedirectConfiguration redirectConfiguration)
        {
            _redirectConfiguration = redirectConfiguration;
            LogQueue = new List<LogEvent>();
        }

        public static RequestLogger Instance
        {
            get { return instanceOverride ?? InternalInstance; }
            set { instanceOverride = value; }
        }

        internal static RequestLogger InternalInstance
        {
            get { return instance; }
        }

        private readonly object SyncRoot = new object();

        private List<LogEvent> LogQueue { get; set; }

        public virtual void LogRequest(string oldUrl, string referrer)
        {
            int bufferSize = _redirectConfiguration.BufferSize;
            lock (SyncRoot)
            {
                if (LogQueue.Count >= bufferSize)
                {
                    try
                    {
                        if (LogQueue.Count >= bufferSize)
                            LogRequests(LogQueue);
                        LogQueue = new List<LogEvent>();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("An error occured whil trying to log 404 errors. ", ex);
                        LogQueue = new List<LogEvent>();
                    }
                }
                LogQueue.Add(new LogEvent(oldUrl, DateTime.Now, referrer));
            }
        }

        private void LogRequests(List<LogEvent> logEvents)
        {
            Logger.Debug("Logging 404 errors to database");
            int bufferSize = _redirectConfiguration.BufferSize;
            int threshold = _redirectConfiguration.ThreshHold;
            DateTime start = logEvents.First().Requested;
            DateTime end = logEvents.Last().Requested;
            int diff = (end - start).Seconds;

            if ((diff != 0 && bufferSize/diff <= threshold) || bufferSize == 0)
            {
                DataAccessBaseEx dba = DataAccessBaseEx.GetWorker();
                foreach (LogEvent logEvent in logEvents)
                {
                    dba.LogRequestToDb(logEvent.OldUrl, logEvent.Referer, logEvent.Requested);
                }
                Logger.Debug(string.Format("{0} 404 request(s) has been stored to the database.", bufferSize));
            }
            else
                Logger.Warning(
                    "404 requests have been made too frequents (exceeded the threshold). Requests not logged to database.");
        }
    }
}