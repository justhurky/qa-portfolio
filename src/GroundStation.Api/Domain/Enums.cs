namespace GroundStation.Api.Domain;

public enum Role
{
    Viewer = 0,
    Operator = 1,
    Supervisor = 2,
    Admin = 3
}

public enum Priority
{
    Routine,
    Priority,
    Emergency
}

public enum BookingState
{
    Requested,
    Confirmed,
    Completed,
    Cancelled
}

public enum BookingAction
{
    Confirm,
    Cancel,
    Complete
}
