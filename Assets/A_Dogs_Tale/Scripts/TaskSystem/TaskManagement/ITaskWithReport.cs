#nullable enable
namespace DogGame.Tasks
{
    public interface ITaskWithReport
    {
        // Must be single-line JSON (no literal newlines).
        bool TryGetReportJson(out string reportJson);
    }
}