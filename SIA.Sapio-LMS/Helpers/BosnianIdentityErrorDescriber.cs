using Microsoft.AspNetCore.Identity;

namespace SIA.Sapio_LMS.Helpers
{
    public class BosnianIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DefaultError()
            => new IdentityError
            {
                Code = nameof(DefaultError),
                Description = "Došlo je do nepoznate greške."
            };

        public override IdentityError ConcurrencyFailure()
            => new IdentityError
            {
                Code = nameof(ConcurrencyFailure),
                Description = "Došlo je do greške zbog istovremene izmjene podataka."
            };

        public override IdentityError PasswordMismatch()
            => new IdentityError
            {
                Code = nameof(PasswordMismatch),
                Description = "Password nije ispravan."
            };

        public override IdentityError InvalidToken()
            => new IdentityError
            {
                Code = nameof(InvalidToken),
                Description = "Token nije ispravan."
            };

        public override IdentityError LoginAlreadyAssociated()
            => new IdentityError
            {
                Code = nameof(LoginAlreadyAssociated),
                Description = "Ovaj login je već povezan sa drugim korisnikom."
            };

        public override IdentityError InvalidUserName(string userName)
            => new IdentityError
            {
                Code = nameof(InvalidUserName),
                Description = $"Korisničko ime '{userName}' nije ispravno."
            };

        public override IdentityError InvalidEmail(string email)
            => new IdentityError
            {
                Code = nameof(InvalidEmail),
                Description = $"Email adresa '{email}' nije ispravna."
            };

        public override IdentityError DuplicateUserName(string userName)
            => new IdentityError
            {
                Code = nameof(DuplicateUserName),
                Description = $"Korisničko ime '{userName}' već postoji."
            };

        public override IdentityError DuplicateEmail(string email)
            => new IdentityError
            {
                Code = nameof(DuplicateEmail),
                Description = $"Email adresa '{email}' već postoji."
            };

        public override IdentityError InvalidRoleName(string role)
            => new IdentityError
            {
                Code = nameof(InvalidRoleName),
                Description = $"Naziv role '{role}' nije ispravan."
            };

        public override IdentityError DuplicateRoleName(string role)
            => new IdentityError
            {
                Code = nameof(DuplicateRoleName),
                Description = $"Rola '{role}' već postoji."
            };

        public override IdentityError UserAlreadyHasPassword()
            => new IdentityError
            {
                Code = nameof(UserAlreadyHasPassword),
                Description = "Korisnik već ima postavljen password."
            };

        public override IdentityError UserLockoutNotEnabled()
            => new IdentityError
            {
                Code = nameof(UserLockoutNotEnabled),
                Description = "Lockout nije omogućen za ovog korisnika."
            };

        public override IdentityError UserAlreadyInRole(string role)
            => new IdentityError
            {
                Code = nameof(UserAlreadyInRole),
                Description = $"Korisnik već ima rolu '{role}'."
            };

        public override IdentityError UserNotInRole(string role)
            => new IdentityError
            {
                Code = nameof(UserNotInRole),
                Description = $"Korisnik nema rolu '{role}'."
            };

        public override IdentityError PasswordTooShort(int length)
            => new IdentityError
            {
                Code = nameof(PasswordTooShort),
                Description = $"Password mora imati najmanje {length} karaktera."
            };

        public override IdentityError PasswordRequiresNonAlphanumeric()
            => new IdentityError
            {
                Code = nameof(PasswordRequiresNonAlphanumeric),
                Description = "Password mora sadržavati najmanje jedan specijalni znak."
            };

        public override IdentityError PasswordRequiresDigit()
            => new IdentityError
            {
                Code = nameof(PasswordRequiresDigit),
                Description = "Password mora sadržavati najmanje jedan broj."
            };

        public override IdentityError PasswordRequiresLower()
            => new IdentityError
            {
                Code = nameof(PasswordRequiresLower),
                Description = "Password mora sadržavati najmanje jedno malo slovo."
            };

        public override IdentityError PasswordRequiresUpper()
            => new IdentityError
            {
                Code = nameof(PasswordRequiresUpper),
                Description = "Password mora sadržavati najmanje jedno veliko slovo."
            };
    }
}