namespace JLic
{
    public enum LicenseErrorCode
    {
        None = 0,
        FileMissing = 1,
        FileInvalid = 2,
        SignatureInvalid = 3,
        IntegrityInvalid = 4,
        DecryptFailed = 5,
        MachineNotLicensed = 6,
        AppDenied = 7,
        Expired = 8,
        KeysMissing = 9
    }
}
