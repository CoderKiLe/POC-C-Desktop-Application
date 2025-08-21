namespace RoutingTutorial
{
    public class RoutingProgram 
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello world");

            string name = "kinley";
            int phoneNumber = 17807306;


            string name1 = "D-kinley";
            int phoneNumber1 = 1234556;

            var anotherClass = new AnotherClass(name, phoneNumber);
            anotherClass.SimpleMethod();


            var anotherClass1 = new AnotherClass(name1, phoneNumber1);
            anotherClass1.SimpleMethod();
        }
    }

    /// <summary>
    /// Just for my trial
    /// </summary>
    public class AnotherClass
    {
        private string? Name { get; }
        private int? PhoneNumber { get;}

        // constructor to initialize everthing
        public AnotherClass(string? name, int? phoneNumber)
        {
            Name = name;
            PhoneNumber = phoneNumber;
        }

        public void SimpleMethod()
        {
            Console.WriteLine($"Name: {Name}");
            Console.WriteLine($"PhoneNumber: {PhoneNumber}");
        }
        public void SimpleMethodw()
        {
            Console.WriteLine("Simple Method 2");
        }
    }

}