using System;

namespace AionFlex.Core.Models;

public record LicenseActivationResult(bool Success, DateTime? ExpiresAt, string Message);
