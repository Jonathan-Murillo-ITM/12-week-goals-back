using _12WeekGoals.Services.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace _12WeekGoals.Services
{
    public interface IAutomatedAuthService
    {
        Task<string> GetAccessTokenWithVisibleBrowserAsync(string username, string password);
    }

    public class AutomatedAuthService : IAutomatedAuthService
    {
        private readonly IMicrosoftGraphService _graphService;

        public AutomatedAuthService(IMicrosoftGraphService graphService)
        {
            _graphService = graphService;
        }

        public async Task<string> GetAccessTokenWithVisibleBrowserAsync(string username, string password)
        {
            var options = new ChromeOptions();
            // NO usar --headless para que sea visible
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1200,800");
            options.AddArgument("--start-maximized");

            using var driver = new ChromeDriver(options);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(120)); // Más tiempo para la verificación manual

            try
            {
                // Obtener la URL de autorización
                var authUrl = await _graphService.GetAuthorizationUrlAsync();
                
                // Navegar a la URL de autorización
                driver.Navigate().GoToUrl(authUrl);

                // Esperar y llenar el campo de email con mejores verificaciones
                var emailField = wait.Until(d => 
                {
                    try
                    {
                        var element = d.FindElement(By.Name("loginfmt"));
                        return element.Displayed && element.Enabled ? element : null;
                    }
                    catch
                    {
                        return null;
                    }
                });
                
                if (emailField == null)
                {
                    throw new Exception("No se pudo encontrar el campo de email");
                }

                // Limpiar el campo antes de escribir
                emailField.Clear();
                await Task.Delay(500); // Pequeña pausa
                emailField.SendKeys(username);
                
                // Hacer clic en "Siguiente" con mejor espera
                var nextButton = wait.Until(d => 
                {
                    try
                    {
                        var element = d.FindElement(By.Id("idSIButton9"));
                        return element.Displayed && element.Enabled ? element : null;
                    }
                    catch
                    {
                        return null;
                    }
                });
                
                if (nextButton == null)
                {
                    throw new Exception("No se pudo encontrar el botón 'Siguiente'");
                }
                
                nextButton.Click();

                // Esperar y llenar el campo de contraseña con verificaciones adicionales
                var passwordField = wait.Until(d => 
                {
                    try
                    {
                        var element = d.FindElement(By.Name("passwd"));
                        return element.Displayed && element.Enabled ? element : null;
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (passwordField == null)
                {
                    throw new Exception("No se pudo encontrar el campo de contraseña");
                }

                // Esperar un poco más y verificar que el elemento esté listo
                await Task.Delay(1000);
                
                // Hacer scroll al elemento para asegurar que esté visible
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", passwordField);
                await Task.Delay(500);

                // Hacer clic en el campo primero para activarlo
                passwordField.Click();
                await Task.Delay(500);
                
                // Limpiar y escribir la contraseña
                passwordField.Clear();
                passwordField.SendKeys(password);
                
                // Hacer clic en "Iniciar sesión"
                var signInButton = wait.Until(d => 
                {
                    try
                    {
                        var element = d.FindElement(By.Id("idSIButton9"));
                        return element.Displayed && element.Enabled ? element : null;
                    }
                    catch
                    {
                        return null;
                    }
                });
                
                if (signInButton == null)
                {
                    throw new Exception("No se pudo encontrar el botón 'Iniciar sesión'");
                }
                
                signInButton.Click();

                // Aquí puede aparecer verificación de dos factores
                // El navegador es visible, así que el usuario puede completar manualmente
                Console.WriteLine("Navegador abierto. Si aparece verificación de 2FA, complétala manualmente.");

                // Esperar hasta que se redirija a localhost (puede tomar tiempo con 2FA)
                wait.Until(d => d.Url.Contains("localhost:5194/callback"));
                
                var currentUrl = driver.Url;
                var match = Regex.Match(currentUrl, @"code=([^&]+)");
                
                if (!match.Success)
                {
                    throw new Exception("No se pudo obtener el código de autorización de la URL");
                }

                var authCode = match.Groups[1].Value;
                
                // Intercambiar el código por un token de acceso
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(authCode);
                
                return accessToken;
            }
            catch (Exception ex)
            {
                // Mantener el navegador abierto por unos segundos para debugging
                await Task.Delay(3000);
                throw new Exception($"Error en la autenticación con navegador visible: {ex.Message}");
            }
            finally
            {
                // Pequeña pausa para que el usuario vea que terminó
                await Task.Delay(2000);
                driver.Quit();
            }
        }
    }
}
