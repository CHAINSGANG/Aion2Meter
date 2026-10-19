using System;

namespace AionFlex.Core.Models;

public record LicenseValidationResult(bool IsValid, DateTime? ExpiresAt, string? Status, string? Email, string Message);
