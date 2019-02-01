namespace Clave.OctoLocal
{
    public static class Colors
    {
        public static string Black(object text)
        {
            return $"\x1B[30m{text}\x1B[39m";
        }

        public static string Red(object text)
        {
            return $"\x1B[31m{text}\x1B[39m";
        }

        public static string Green(object text)
        {
            return $"\x1B[32m{text}\x1B[39m";
        }

        public static string Yellow(object text)
        {
            return $"\x1B[33m{text}\x1B[39m";
        }

        public static string Blue(object text)
        {
            return $"\x1B[34m{text}\x1B[39m";
        }

        public static string Magenta(object text)
        {
            return $"\x1B[35m{text}\x1B[39m";
        }

        public static string Cyan(object text)
        {
            return $"\x1B[36m{text}\x1B[39m";
        }

        public static string White(object text)
        {
            return $"\x1B[37m{text}\x1B[39m";
        }

        public static string Bold(object text)
        {
            return $"\x1B[1m{text}\x1B[22m";
        }
    }
}