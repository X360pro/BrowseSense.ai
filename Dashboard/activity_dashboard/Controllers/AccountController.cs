using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Mvc;
using activity_dashboard.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace activity_dashboard.Controllers
{
    [Route("[controller]")]
    public class AccountController : Controller
    {
        private readonly IAmazonCognitoIdentityProvider _cognitoProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAmazonCognitoIdentityProvider cognitoProvider,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _cognitoProvider = cognitoProvider;
            _configuration = configuration;
            _logger = logger;
        }

        private string CalculateSecretHash(string username)
        {
            var clientSecret = _configuration["AWS:ClientSecret"];
            var clientId = _configuration["AWS:ClientId"];
            
            var message = Encoding.UTF8.GetBytes(username + clientId);
            var key = Encoding.UTF8.GetBytes(clientSecret);
            
            using (var hmac = new HMACSHA256(key))
            {
                var hash = hmac.ComputeHash(message);
                return Convert.ToBase64String(hash);
            }
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var accessToken = HttpContext.Session.GetString("AccessToken");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Sign out from Cognito
                    var signOutRequest = new GlobalSignOutRequest
                    {
                        AccessToken = accessToken
                    };
                    await _cognitoProvider.GlobalSignOutAsync(signOutRequest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
            }
            finally
            {
                // Clear all session data
                HttpContext.Session.Clear();
            }

            return RedirectToAction("Login");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var authRequest = new InitiateAuthRequest
                {
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                    ClientId = _configuration["AWS:ClientId"],
                    AuthParameters = new Dictionary<string, string>
                    {
                        {"USERNAME", model.Email},
                        {"PASSWORD", model.Password},
                        {"SECRET_HASH", CalculateSecretHash(model.Email)}
                    }
                };

                var response = await _cognitoProvider.InitiateAuthAsync(authRequest);

                if (response.AuthenticationResult != null)
                {
                    // Store tokens in session
                    HttpContext.Session.SetString("IdToken", response.AuthenticationResult.IdToken);
                    HttpContext.Session.SetString("AccessToken", response.AuthenticationResult.AccessToken);
                    HttpContext.Session.SetString("UserEmail", model.Email);

                    return RedirectToAction("Index", "TestConnection");
                }

                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }
            catch (NotAuthorizedException)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }
            catch (UserNotConfirmedException)
            {
                TempData["WarningMessage"] = "Please confirm your email first.";
                return RedirectToAction("ConfirmEmail", new { email = model.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        [HttpGet("signup")]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup(SignupModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var signUpRequest = new SignUpRequest
                {
                    ClientId = _configuration["AWS:ClientId"],
                    Username = model.Email,
                    Password = model.Password,
                    SecretHash = CalculateSecretHash(model.Email),
                    UserAttributes = new List<AttributeType>
                    {
                        new AttributeType { Name = "email", Value = model.Email }
                    }
                };

                var response = await _cognitoProvider.SignUpAsync(signUpRequest);
                
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    TempData["SuccessMessage"] = "Registration successful! Please check your email for verification.";
                    return RedirectToAction("ConfirmEmail", new { email = model.Email });
                }

                ModelState.AddModelError("", "Registration failed. Please try again.");
                return View(model);
            }
            catch (UsernameExistsException)
            {
                ModelState.AddModelError("", "An account with this email already exists.");
                return View(model);
            }
            catch (InvalidPasswordException)
            {
                ModelState.AddModelError("Password", "Password must be at least 8 characters long and contain uppercase, lowercase, numbers, and special characters.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during signup for user {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        [HttpGet("confirm-email")]
        public IActionResult ConfirmEmail(string email)
        {
            var model = new ConfirmEmailModel { Email = email };
            return View(model);
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var confirmSignUpRequest = new ConfirmSignUpRequest
                {
                    ClientId = _configuration["AWS:ClientId"],
                    Username = model.Email,
                    ConfirmationCode = model.Code,
                    SecretHash = CalculateSecretHash(model.Email)
                };

                var response = await _cognitoProvider.ConfirmSignUpAsync(confirmSignUpRequest);
                
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    TempData["SuccessMessage"] = "Email confirmed successfully! You can now login.";
                    return RedirectToAction("Login");
                }
            }
            catch (CodeMismatchException)
            {
                ModelState.AddModelError("Code", "Invalid verification code. Please try again.");
            }
            catch (ExpiredCodeException)
            {
                ModelState.AddModelError("Code", "Verification code has expired. Please request a new one.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming email for user {Email}", model.Email);
                ModelState.AddModelError("", "Error confirming email. Please try again.");
            }

            return View(model);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Attempt to initiate the forgot password process
                var forgotPasswordRequest = new ForgotPasswordRequest
                {
                    ClientId = _configuration["AWS:ClientId"],
                    Username = model.Email,
                    SecretHash = CalculateSecretHash(model.Email)
                };

                await _cognitoProvider.ForgotPasswordAsync(forgotPasswordRequest);

                TempData["SuccessMessage"] = "If an account exists with this email, a password reset code has been sent.";
                return RedirectToAction("ResetPassword", new { email = model.Email });
            }
            catch (UserNotFoundException)
            {
                // Provide feedback that the email does not exist
                ModelState.AddModelError("", "No account exists with this email address.");
                return View(model);
            }
            catch (UserNotConfirmedException)
            {
                ModelState.AddModelError("", "Please confirm your email address first.");
                return View(model);
            }
            catch (LimitExceededException)
            {
                ModelState.AddModelError("", "Too many attempts. Please try again later.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for user {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred. Please try again later.");
                return View(model);
            }
        }
        [HttpGet("reset-password")]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            var model = new ResetPasswordModel { Email = email };
            return View(model);
        }
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var resetPasswordRequest = new ConfirmForgotPasswordRequest
                {
                    ClientId = _configuration["AWS:ClientId"],
                    Username = model.Email,
                    Password = model.NewPassword,
                    ConfirmationCode = model.Code,
                    SecretHash = CalculateSecretHash(model.Email)
                };

                await _cognitoProvider.ConfirmForgotPasswordAsync(resetPasswordRequest);

                TempData["SuccessMessage"] = "Password has been reset successfully. Please login with your new password.";
                return RedirectToAction("Login");
            }
            catch (CodeMismatchException)
            {
                ModelState.AddModelError("Code", "Invalid verification code. Please try again.");
            }
            catch (ExpiredCodeException)
            {
                ModelState.AddModelError("Code", "Verification code has expired. Please request a new one.");
                return RedirectToAction("ForgotPassword");
            }
            catch (InvalidPasswordException)
            {
                ModelState.AddModelError("NewPassword", 
                    "Password must be at least 8 characters long and contain uppercase, lowercase, numbers, and special characters.");
            }
            catch (LimitExceededException)
            {
                ModelState.AddModelError("", "Too many attempts. Please try again later.");
            }
            catch (UserNotFoundException)
            {
                ModelState.AddModelError("", "Invalid request. Please start the password reset process again.");
                return RedirectToAction("ForgotPassword");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred. Please try again.");
            }

            return View(model);
        }

        [HttpGet("resend-confirmation-code")]
        public IActionResult ResendConfirmationCode(string email)
        {
            try
            {
                var resendRequest = new ResendConfirmationCodeRequest
                {
                    ClientId = _configuration["AWS:ClientId"],
                    Username = email,
                    SecretHash = CalculateSecretHash(email)
                };

                _cognitoProvider.ResendConfirmationCodeAsync(resendRequest);
                TempData["SuccessMessage"] = "A new verification code has been sent to your email.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending confirmation code for user {Email}", email);
                TempData["ErrorMessage"] = "Failed to send new verification code. Please try again.";
            }

            return RedirectToAction("ConfirmEmail", new { email });
        }

        

        [HttpGet("forgot-password")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // [HttpPost("forgot-password")]
        // public async Task<IActionResult> ForgotPassword(ForgotPasswordModel model)
        // {
        //     if (!ModelState.IsValid)
        //     {
        //         return View(model);
        //     }

        //     try
        //     {
        //         var forgotPasswordRequest = new ForgotPasswordRequest
        //         {
        //             ClientId = _configuration["AWS:ClientId"],
        //             Username = model.Email,
        //             SecretHash = CalculateSecretHash(model.Email)
        //         };

        //         await _cognitoProvider.ForgotPasswordAsync(forgotPasswordRequest);
                
        //         // Store email in TempData for the reset page
        //         TempData["ResetEmail"] = model.Email;
        //         TempData["SuccessMessage"] = "Password reset code has been sent to your email.";
                
        //         return RedirectToAction("ResetPassword");
        //     }
        //     catch (UserNotFoundException)
        //     {
        //         // For security, we show the same message even if the user doesn't exist
        //         TempData["SuccessMessage"] = "If an account exists with this email, a password reset code has been sent.";
        //         return RedirectToAction("Login");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error initiating password reset for user {Email}", model.Email);
        //         ModelState.AddModelError("", "An error occurred. Please try again later.");
        //         return View(model);
        //     }
        // }
    }
}