namespace HtmlToPdfConsole
{
    using System;
    using System.Threading.Tasks;

    using Converter;

    internal class Program
    {
        static async Task Main(string[] args)
        {
            try 
            {
                /*
                using var converter = await PuppeteerConverter.CreateAsync();

                var pathToPdf = await converter.ConvertAsync(@"C:\temp\11\sample.html");
                var pathToPdf2 = await converter.ConvertAsync(@"C:\temp\11\sample.html");
                var pathToPdf3 = await converter.ConvertAsync(@"C:\temp\11\sample.html");

                var i = 1;
                */


                using var client = new ConverterClient();
                client.AddConvertFile("file1");
                client.AddConvertFile("file2");

                Console.ReadLine();
            }
            catch(Exception e)
            {
                var x = e;
            }
        }
    }
}

/*
Варианты работы:
Пользователь загружает файл
    Получил идентификатор
        Есть идентификатор процесса
    Не получил идентификатор
        Исключение

Пользователь опрашивает завершился ли процесс по идентификатору
    Процесс завершился
        Пользователь запрашивает результат по идентификатору
            Получает файл
            Файл не найден
                Исключение
    Процесс работает
        Ждём
    Процесс не найден
        Исключение
 */