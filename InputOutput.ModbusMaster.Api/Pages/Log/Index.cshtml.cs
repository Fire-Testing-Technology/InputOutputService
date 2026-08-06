using InputOutput.ModbusMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InputOutput.ModbusMaster.Api.Pages.Log;

public sealed class IndexModel(DiagnosticLogService logService) : PageModel
{
    public IReadOnlyList<DiagnosticLogEntry> Entries { get; private set; } = [];

    public void OnGet() => Entries = logService.Snapshot().Reverse().ToArray();

    public IActionResult OnPostClear()
    {
        logService.Clear();
        TempData["Message"] = "Diagnostic log cleared.";
        return RedirectToPage();
    }
}
