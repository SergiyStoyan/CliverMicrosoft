//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cliver
{
    /// <summary>
    /// Trier base class adapted for Microsoft API
    /// </summary>
    public class MicrosoftTrier
    {
        virtual public List<System.Net.HttpStatusCode> RetriableHttpCodes { get; } = new List<System.Net.HttpStatusCode> {
            System.Net.HttpStatusCode.InternalServerError,
            System.Net.HttpStatusCode.Gone,
            System.Net.HttpStatusCode.BadRequest,
        };

        virtual public int DefaultTryMaxNumber { get; } = 3;
        virtual public int DefaultRetryDelayMss { get; } = 10000;

        /// <summary>
        /// Trier adapted for microsoft API requests. Can be used as a framework.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logMessage"></param>
        /// <param name="function"></param>
        /// <param name="maxTryNumber"></param>
        /// <param name="retryDelayMss"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        /// <returns></returns>
        /// <exception cref="Exception2"></exception>
        virtual public T Run<T>(string logMessage, Func<T> function, int maxTryNumber = -1, int retryDelayMss = -1, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null) where T : class
        {
            if (maxTryNumber < 0)
                maxTryNumber = DefaultTryMaxNumber;
            if (retryDelayMss < 0)
                retryDelayMss = DefaultRetryDelayMss;
            List<System.Net.HttpStatusCode> retriableHttpCodes = RetriableHttpCodes;
            if (additionalRetriableHttpCodes != null)
                retriableHttpCodes.AddRange(additionalRetriableHttpCodes);
            if (logMessage != null)
                Log.Inform(logMessage);
            T o = SleepRoutines.WaitForObject(
                () =>
                {
                    try
                    {
                        return function();
                    }
                    catch (Exception e)
                    {
                        for (; e != null; e = e.InnerException)
                            if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException ex && retriableHttpCodes.Contains((System.Net.HttpStatusCode)ex.ResponseStatusCode))
                            {
                                Log.Warning2("Retrying...\r\n" + logMessage, e);
                                return null;
                            }
                        throw;
                    }
                },
                0, retryDelayMss, false, maxTryNumber
            );
            if (o == null)
            {
                string m = logMessage != null ? Regex.Replace(logMessage, @"\.\.\.", "") : nameof(MicrosoftTrier) + "." + nameof(Run) + "()";
                throw new Exception2("Failed: " + m);
            }
            return o;
        }

        /// <summary>
        /// Trier adapted for microsoft API requests. Can be used as a framework.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="function"></param>
        /// <param name="maxTryNumber"></param>
        /// <param name="retryDelayMss"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        /// <returns></returns>
        virtual public T Run<T>(Func<T> function, int maxTryNumber = -1, int retryDelayMss = -1, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null) where T : class
        {
            return Run(null, function, maxTryNumber, retryDelayMss, additionalRetriableHttpCodes);
        }

        /// <summary>
        /// Trier adapted for microsoft API requests. Can be used as a framework.
        /// </summary>
        /// <param name="logMessage"></param>
        /// <param name="action"></param>
        /// <param name="maxTryNumber"></param>
        /// <param name="retryDelayMss"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        virtual public void Run(string logMessage, Action action, int maxTryNumber = -1, int retryDelayMss = -1, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null)
        {
            Run(logMessage, () => { action(); return new Object(); }, maxTryNumber, retryDelayMss, additionalRetriableHttpCodes);
        }

        /// <summary>
        /// Trier adapted for microsoft API requests. Can be used as a framework.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maxTryNumber"></param>
        /// <param name="retryDelayMss"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        virtual public void Run(Action action, int maxTryNumber = -1, int retryDelayMss = -1, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null)
        {
            Run(null, action, maxTryNumber, retryDelayMss, additionalRetriableHttpCodes);
        }
    }
}