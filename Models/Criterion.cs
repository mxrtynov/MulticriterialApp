namespace MulticriterialApp.Models
{
    public enum CriterionType { Max, Min }

    public class Criterion
    {
        public string Name { get; set; } = "";
        public CriterionType Type { get; set; }
        public double Weight { get; set; }

        public Criterion() { }
        public Criterion(string name, CriterionType type)
        {
            Name = name;
            Type = type;
        }
    }
}