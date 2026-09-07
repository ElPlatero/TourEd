namespace TourEd.Lib.Abstractions.Models;

public record UserDataImportError(int? Line, string Message);

// An error rejects the entire file; Imported and Existing are only counted after successful validation.
public record UserDataImportResult(int Imported, int Existing, int Rejected, IReadOnlyList<UserDataImportError> Errors);
