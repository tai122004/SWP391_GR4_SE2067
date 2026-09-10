using Azure;

namespace RWPM.Common
{
    public static class EventStreamHelper
    {
        public static string MESSAGE_DONE = "DONE";
        public static string MESSAGE_PROGRESS_PERCENT = "PROGRESS_PERCENT=";

        public static void SSEInit(HttpResponse httpResponse)
        {
            httpResponse.ContentType = "text/event-stream";
            httpResponse.Headers.Append("Cache-Control", "no-cache");
        }

        public async static Task WriteDoneMessage(HttpResponse httpResponse, CancellationToken cancellationToken)
        {
            await httpResponse.WriteAsync($"data: {MESSAGE_DONE}\n\n", cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public async static Task WriteProgressPercentMessage(HttpResponse httpResponse, int progressPercent, CancellationToken cancellationToken)
        {
            await httpResponse.WriteAsync($"data: {MESSAGE_PROGRESS_PERCENT}{progressPercent}\n\n", cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public async static Task WriteMessage(HttpResponse httpResponse, string message, CancellationToken cancellationToken)
        {
            await httpResponse.WriteAsync($"data: {message}\n\n", cancellationToken);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }
    }
}
