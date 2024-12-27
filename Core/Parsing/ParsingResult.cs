#region

using System;

#endregion

namespace IZ.Core.Parsing;

public class ParsingResult {

  public static readonly ParsingResult Parsed = new ParsingResult(ParsingStatus.Parsed);
  public static readonly ParsingResult EmptyInputString = new ParsingResult(ParsingStatus.EmptyInputString);
  public static readonly ParsingResult NotMatched = new ParsingResult(ParsingStatus.NotMatched);

  private readonly string? _error;

  private ParsingResult(string error)
    : this(ParsingStatus.FormatError, error) { }

  private ParsingResult(ParsingStatus status)
    : this(status, null) { }

  private ParsingResult(ParsingStatus status, string? error = null) {
    Status = status;
    _error = error;
  }


  public ParsingStatus Status { get; }

  public Exception? Exception {
    get {
      switch (Status) {
        case ParsingStatus.EmptyInputString:
          return new ArgumentException("Input string is null or contains white-spaces only.");
        case ParsingStatus.NotMatched:
          return new FormatException("Input string has invalid format.");
        case ParsingStatus.FormatError:
          return new FormatException(_error);
      }

      return null;
    }
  }

  public static ParsingResult Error(string error) => new ParsingResult(error);
}
