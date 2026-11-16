using System;

// Core contract for business-level success/failure (core-domain.md 0., 1.).
// Exceptions are reserved for programmer errors; any expected failure
// (e.g. "DIVISION_BY_ZERO") is reported through Result<T> so headless
// simulation stays robust.
namespace MathHorde.Core.Common
{
    /// <summary>
    /// Success/failure result carrying either a value or an error code.
    /// Value is intentionally not a plain stored field: reading it on a
    /// failed result throws rather than silently handing back default(T),
    /// which could otherwise be mistaken for a real value (e.g. 0 looking
    /// like a valid math answer).
    /// </summary>
    public readonly struct Result<T>
    {
        private readonly T _value;

        public bool IsSuccess { get; }

        /// <summary>Valid only when IsSuccess is true; throws otherwise.</summary>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Result<{typeof(T).Name}> failed with error code '{ErrorCode}'; Value is not valid.");
                }

                return _value;
            }
        }

        /// <summary>Null on success; a short machine-readable code (e.g. "DIVISION_BY_ZERO") on failure.</summary>
        public string ErrorCode { get; }

        private Result(bool isSuccess, T value, string errorCode)
        {
            IsSuccess = isSuccess;
            _value = value;
            ErrorCode = errorCode;
        }

        public static Result<T> Ok(T value) => new Result<T>(true, value, null);

        public static Result<T> Fail(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentException("errorCode must be a non-empty code.", nameof(errorCode));
            }

            return new Result<T>(false, default, errorCode);
        }
    }
}
