using System;
using System.Globalization;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;


namespace StockAlert

{
    class Program
    {

        
        static async Task Main(string[] args)
        {
            if (args.Length != 3) // verifica se os três parâmetros foram passados 
            {
                Console.WriteLine("Passe os três parâmetros corretamente");
                Console.WriteLine("Exemplo: StockAlert PETR4 22.67 22.59");
                return;

            }
            string ticker = args[0].ToUpper(); // passa a primeira palavra para maiuscula para evitar erros de leitura
            if (!double.TryParse(args[1], NumberStyles.Any,
               CultureInfo.InvariantCulture, out double sellAt) ||
           !double.TryParse(args[2], NumberStyles.Any,
               CultureInfo.InvariantCulture, out double buyAt)) //tenta passar os numeros para double e ve se tem erro
            {
                Console.WriteLine("Erro: preços inválidos. Use ponto como separador decimal.");
                return;
            }

            Console.WriteLine($"Monitorando: {ticker}");
            Console.WriteLine($"Vender acima de: R$ {sellAt}");
            Console.WriteLine($"Comprar abaixo de: R$ {buyAt}");
            string jsonString = File.ReadAllText("appsettings.json");
            var jsonDoc =JsonDocument.Parse(jsonString);
            var email = jsonDoc.RootElement.GetProperty("Email");

            string smtpHost = email.GetProperty("SmtpHost").GetString()!;
            int smtpPort = email.GetProperty("SmtpPort").GetInt32();
            string emailFrom = email.GetProperty("From").GetString()!;
            string emailPass = email.GetProperty("Password").GetString()!;
            string emailTo= email.GetProperty("To").GetString()!;

            Console.WriteLine($"Configuração carregada! Enviando alertas para: {emailTo}");
            Console.WriteLine("Monitoramento iniciado");
            string lastState = "normal";
            while (true)
            {
                using var httpClient = new HttpClient();
                string url = $"https://brapi.dev/api/quote/{ticker}";
                string response = await httpClient.GetStringAsync(url);

                var jsonResponse = JsonDocument.Parse(response);
                double currentPrice = jsonResponse.RootElement
                    .GetProperty("results")[0]
                    .GetProperty("regularMarketPrice")
                    .GetDouble();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Cotação de {ticker}: R${currentPrice}");
                
                if (currentPrice >= sellAt&&lastState!="sell")
                {
                    lastState = "sell";
                    Console.WriteLine($"ALERTA: Preço atingiu R${currentPrice}. Hora de vender");
                    SendEmail(smtpHost, smtpPort, emailFrom, emailPass, emailTo,
                        subject: $"[VENDA]{ticker} atingiu R$ {currentPrice}",
                        body: $"O ativo {ticker} esta em R${currentPrice},acima do limite de venda R${sellAt}");

                }
                else if (currentPrice <= buyAt&&lastState!="buy")
                {
                    lastState = "buy";
                    Console.WriteLine($"Alerta: Preço caiu para R${currentPrice}, hora de comprar!");
                    SendEmail(smtpHost, smtpPort, emailFrom, emailPass, emailTo,
                        subject: $"[COMPRA] {ticker} atingiu R${currentPrice}",
                        body: $"O ativo {ticker} atingiu R${currentPrice}, está na hora de comprar.");
                }

                else if (currentPrice<sellAt&&currentPrice>buyAt&&lastState!="normal")
                {
                    lastState = "normal";
                    Console.WriteLine("Preço dentro do intervalo padrão.");
                }
                await Task.Delay(TimeSpan.FromSeconds(15));
                

            }




           

          


        }
        static void SendEmail(string host, int port, string from, string password, string to, string subject, string body)
        {
            using var smtp = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(from, password),
                EnableSsl = true
            };
            var message = new MailMessage(from, to, subject, body);
            smtp.Send(message);
            Console.WriteLine("Email de alerta enviado");
        }
    }
}