namespace AonGenerator
{
    internal class Options
    {
        public string DbHost { get; set; }
        public string DbDatabase { get; set; }
        public string DbUser { get; set; }
        public string DbPassword { get; set; }
        public string OutputPath { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Options() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }
}
