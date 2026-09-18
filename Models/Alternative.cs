namespace MulticriterialApp.Models
{
    public class Alternative
    {
        public string Name { get; set; } = "";
        public double[] Scores { get; set; } = System.Array.Empty<double>();

        public Alternative() { }
        public Alternative(string name, params double[] scores)
        {
            Name = name;
            Scores = scores;
        }
    }
}