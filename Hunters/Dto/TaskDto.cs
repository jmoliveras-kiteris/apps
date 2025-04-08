namespace Hunters.Dto
{
    public class TaskDto
    {
        public string TaskId { get; set; }
        public string TaskName { get; set; }
        public string DepositName { get; set; }
        public string Progress { get; set; }
        public string Priority { get; set; }
        public string Assignee { get; set; }
        public string Creator { get; set; }
        public string CreatedDate { get; set; }
        public string BeginDate { get; set; }
        public string DueDate { get; set; }
        public bool Periodic { get; set; }
        public bool Delayed { get; set; }
        public string EndDate { get; set; }
        public string CompletedBy { get; set; }
        public string Elements { get; set; }
        public string ComprobationElements { get; set; }
        public string Tags { get; set; }
        public string Description { get; set; }
    }
}
