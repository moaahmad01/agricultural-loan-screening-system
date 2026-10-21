namespace AgriculturalLoanSystem.Data.Enums
{
    public enum ClientType
    {
        Individual,
        Commercial
    }
    public enum LoanApplicationStatus
    {
        PendingApproval = 1, Approved = 2, Rejected = 3, Disbursed = 4
    }
    public enum LoanCategory
    {
        SeasonalShortTerm = 1,

        MediumLongTerm = 2
    }
    public enum LoanStatus
    {
        Paid = 1, Delayed = 2, Ongoing = 3, Defaulted = 4
    }


    public enum SessionStatus
    {
        InProgress = 1, Completed = 2, Abandoned = 3
    }

    public enum Recommendation
    {
        Eligible = 1, EligibleWithConditions = 2, NotEligible = 3
    }

    public enum AnswerType
    {
        No = 0,
        Yes = 1
    }

    public enum PaymentFrequency
    {
        Weekly = 1,
        Biweekly = 2,
        Monthly = 3,
        Bimonthly = 4,
        Quarterly = 5,
        Semiannual = 6,
        Annual = 7
    }

    public enum InterestType
    {
        Simple = 1, Reducing = 2, Murabaha = 3
    }
}