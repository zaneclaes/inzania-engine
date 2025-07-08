namespace IZ.Server.Emails;

public enum EmailValidationVerdict {
  Unknown = -2,
  Invalid = -1,
  Risky = 0,
  Valid = 1
}
