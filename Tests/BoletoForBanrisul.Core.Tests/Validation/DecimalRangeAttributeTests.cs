using BoletoForBanrisul.Core.Validation;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// <see cref="DecimalRangeAttribute"/> exists because <see cref="RangeAttribute"/>'s decimal form fails in
    /// two ways that only show up in production: it parses its string bounds with the current culture, and it
    /// converts them lazily by mutating itself, which is not safe when several threads validate at once.
    /// </summary>
    [Trait("Category", "Unit")]
    public class DecimalRangeAttributeTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(0.01)]
        [InlineData(500)]
        [InlineData(999.99)]
        public void ValuesInsideTheRange_AreAccepted(decimal value) =>
            Assert.True(new DecimalRangeAttribute("0", "999.99").IsValid(value));

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1000)]
        public void ValuesOutsideTheRange_AreRejected(decimal value) =>
            Assert.False(new DecimalRangeAttribute("0", "999.99").IsValid(value));

        [Fact]
        public void Null_IsAccepted() =>
            // Absence is [Required]'s business, not a range's.
            Assert.True(new DecimalRangeAttribute("0", "999.99").IsValid(null));

        [Fact]
        public void TheLargestBoundInUse_SurvivesConstructionExactly()
        {
            // 999999999999.99 (BanrisulBounds.MoneyMax) has more significant digits than a double holds
            // exactly, which is why the bounds are taken as text rather than as double literals.
            var attribute = new DecimalRangeAttribute(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax);

            Assert.Equal(999999999999.99m, attribute.Maximum);
            Assert.True(attribute.IsValid(999999999999.99m));
            Assert.False(attribute.IsValid(1000000000000.00m));
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("de-DE")]
        [InlineData("en-US")]
        public void BoundsAreReadInvariantly_WhateverTheCurrentCulture(string culture)
        {
            // RangeAttribute's decimal form parses its bounds with CurrentCulture, so under a comma-decimal
            // locale "999999999999.99" throws instead of validating. Constructing under each culture here is
            // what pins that this one does not care.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

                var attribute = new DecimalRangeAttribute(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax);

                Assert.Equal(999999999999.99m, attribute.Maximum);
                Assert.True(attribute.IsValid(1234.56m));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void AMalformedBound_FailsAtConstruction()
        {
            // Loudly, at startup, rather than on the first request that happens to validate.
            Assert.Throws<ArgumentException>(() => new DecimalRangeAttribute("0", "not a number"));
        }

        [Fact]
        public void NoModelProperty_UsesRangeAttributesStringBoundsForm()
        {
            // The guard that actually holds the line. The concurrency test below demonstrates the failure,
            // but only catches a revert *probabilistically*: the race window is a few instructions wide, so
            // whether it is hit depends on machine load and on what else happens to be running. This test
            // catches the revert every time, in microseconds.
            //
            // Only the string-bounds form is a problem. [Range(0, 99)] with int literals (Protesto.Prazo,
            // Baixa.Prazo, PagamentoParcial.Quantidade) is safe: its lazy conversion rewrites the bounds to
            // the same ints they already are, so a thread arriving mid-way reads an int where it expects one.
            var offenders = typeof(Core.Models.Boleto).Assembly.GetTypes()
                .SelectMany(type => type.GetProperties())
                .Select(property => new
                {
                    Property = property,
                    Range = (RangeAttribute?)Attribute.GetCustomAttribute(property, typeof(RangeAttribute))
                })
                .Where(candidate => candidate.Range?.Minimum is string)
                .Select(candidate => $"{candidate.Property.DeclaringType!.Name}.{candidate.Property.Name}")
                .ToList();

            Assert.True(
                offenders.Count == 0,
                "RangeAttribute's string-bounds form is not thread-safe and is parsed with the current " +
                "culture. Use [DecimalRange] instead. Offending: " + string.Join(", ", offenders));
        }

        [Fact]
        public void OneSharedInstance_IsSafeToUseFromManyThreadsAtOnce()
        {
            // The regression this attribute was written for, tested as the property that actually matters:
            // a single attribute instance, used by many threads, none of which has validated with it yet.
            // That is how validation attributes are really used -- MiniValidation caches PropertyDetails
            // (attribute instances included) in a static cache, so every thread validating a Titulo shares
            // one instance per property.
            //
            // RangeAttribute's decimal form fails exactly here: it converts its string bounds on first use by
            // overwriting its own Minimum/Maximum, so a second thread can read a bound the first has already
            // converted and throw InvalidCastException ("Unable to cast object of type 'System.Decimal' to
            // type 'System.String'") out of validation instead of returning a verdict.
            //
            // This drives the attribute directly rather than going through RecursiveValidator: through the
            // validator the race does not reproduce reliably, because a cold type has every thread build its
            // own copy of the metadata and only race to publish it. The window is a few instructions wide, so
            // a fresh instance is raced repeatedly and the threads are released by a Barrier rather than
            // merely started together.
            const int iterations = 400;
            const int threads = 8;
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var verdicts = new System.Collections.Concurrent.ConcurrentBag<bool>();

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var attribute = new DecimalRangeAttribute(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax);
                using var barrier = new Barrier(threads);

                var workers = Enumerable.Range(0, threads).Select(i => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        verdicts.Add(attribute.IsValid(i % 2 == 0 ? 1234.56m : -1m));
                    }
                    catch (Exception exception)
                    {
                        exceptions.Add(exception);
                    }
                })).ToList();

                foreach (var worker in workers)
                    worker.Start();

                foreach (var worker in workers)
                    Assert.True(worker.Join(TimeSpan.FromSeconds(30)), "A validation thread did not finish.");
            }

            Assert.Empty(exceptions);

            // Not just "did not throw": the verdicts must be right too, half valid and half not, so an
            // implementation made thread-safe by never deciding anything would still fail here.
            Assert.Equal(iterations * threads, verdicts.Count);
            Assert.Equal(iterations * threads / 2, verdicts.Count(v => v));
            Assert.Equal(iterations * threads / 2, verdicts.Count(v => !v));
        }
    }
}
