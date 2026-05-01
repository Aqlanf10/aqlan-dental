# Sprint 17B — Language, Currency, and Exchange Rate Settings

## Goal

Add a controlled system-wide settings module for language and multi-currency financial handling.

This sprint must be implemented after core finance basics are stable, because it affects payments, receipts, invoices, account statements, financial reports, and patient balances.

## Main Requirements

1. The system must allow changing the interface language from Settings.
2. The default language remains Arabic RTL.
3. The system must be prepared for additional languages later without redesigning the UI.
4. The base accounting currency is Yemeni Rial (YER).
5. The system must support patient payments in:
   - Yemeni Rial (YER)
   - Saudi Riyal (SAR)
   - US Dollar (USD)
6. The system must support exchange rates against Yemeni Rial.
7. Payments in SAR or USD must be converted to YER for accounting and reporting.
8. The original paid currency and original amount must always be preserved.
9. Patient account statements must show both the original currency and the YER equivalent.
10. Financial reports must be able to show:
    - totals in YER
    - totals by original currency
    - exchange-rate differences when applicable

## Important Accounting Rule

YER is the base ledger currency.

Any payment entered in SAR or USD must store:

- OriginalAmount
- OriginalCurrency
- ExchangeRateToYER
- AmountInYER
- ExchangeRateDate
- ExchangeRateSource

Do not overwrite original values after conversion.

## Required Settings

### Language Settings

Add settings for:

- DefaultLanguage: ar
- SupportedLanguages: ar, en later
- Direction: rtl for Arabic, ltr for English later
- DateFormat
- NumberFormat

### Currency Settings

Add settings for:

- BaseCurrency: YER
- EnabledCurrencies:
  - YER
  - SAR
  - USD
- DefaultPaymentCurrency: YER
- AllowManualExchangeRate: true
- RequireExchangeRateApproval: optional later

## Suggested Database Schema

### Currency

```text
Currency
- Id
- Code              // YER, SAR, USD
- NameAr            // ريال يمني، ريال سعودي، دولار أمريكي
- NameEn            // Yemeni Rial, Saudi Riyal, US Dollar
- Symbol            // YER, SAR, USD or local symbol
- IsBaseCurrency
- IsEnabled
- DecimalPlaces
- CreatedAt
- UpdatedAt
```

### ExchangeRate

```text
ExchangeRate
- Id
- FromCurrencyCode  // SAR or USD
- ToCurrencyCode    // YER
- Rate              // e.g. 1 SAR = X YER
- RateDate
- Source            // Manual, CentralBank, Market, Other
- Notes
- CreatedBy
- ApprovedBy        // optional
- IsActive
- CreatedAt
- UpdatedAt
```

### Payment Currency Fields

Payments should support:

```text
Payment
- AmountOriginal
- CurrencyCode
- ExchangeRateToYER
- AmountInYER
- ExchangeRateId optional
- ExchangeRateDate
```

Keep existing `Amount` temporarily if needed for compatibility, but define clearly whether it means YER or original amount.

Preferred final meaning:

- `AmountOriginal` = what the patient actually paid
- `CurrencyCode` = currency used by patient
- `AmountInYER` = accounting amount in base currency

### Invoice / Contract Currency Fields

Contracts and invoices should remain primarily in YER unless explicitly configured otherwise.

Add support for:

```text
Contract
- TotalAmountYER
- DisplayCurrency optional

Invoice
- TotalAmountYER
- DisplayCurrency optional
```

## Required Backend Features

1. Add currencies seed data:
   - YER base currency
   - SAR enabled
   - USD enabled
2. Add CRUD endpoints for exchange rates.
3. Add endpoint to get latest rate:

```text
GET /api/settings/currencies
GET /api/settings/exchange-rates/latest?from=SAR&to=YER
POST /api/settings/exchange-rates
PUT /api/settings/exchange-rates/{id}
```

4. Add validation:
   - If payment currency is YER, exchange rate = 1.
   - If payment currency is SAR or USD, exchange rate is required.
   - AmountInYER = AmountOriginal × ExchangeRateToYER.
   - Exchange rate must be positive.
   - Currency must be enabled.
5. Add audit log for exchange-rate changes.
6. Add permission:
   - FinanceSettings.View
   - FinanceSettings.Edit
   - ExchangeRates.Create
   - ExchangeRates.Edit

## Required Frontend Features

### Settings Page

Add settings sections:

1. Language & Display
2. Currency & Exchange Rates

### Language UI

- Default language selector.
- Arabic must stay RTL.
- English can be marked as future/prepared if not fully translated.
- Do not break current Arabic UI.

### Currency UI

- Show base currency: Yemeni Rial (YER).
- Enable/disable SAR and USD.
- Add exchange rate table.
- Add new exchange rate form.
- Show latest exchange rate for SAR → YER and USD → YER.

### Payment Form

When entering payment:

1. User selects currency: YER / SAR / USD.
2. If YER:
   - exchange rate = 1
   - amount in YER = same amount
3. If SAR or USD:
   - show latest exchange rate
   - allow manual rate entry if permitted
   - calculate YER equivalent live
4. Save both original amount and YER equivalent.

### Receipts

Receipt must show:

- Paid amount in original currency
- Exchange rate used
- Equivalent amount in YER
- Remaining balance in YER

Example:

```text
المبلغ المدفوع: 100 ريال سعودي
سعر الصرف: 1 ريال سعودي = 140 ريال يمني
المعادل بالريال اليمني: 14,000 ريال يمني
```

### Account Statement

Patient account statement must show columns:

- Date
- Description
- Debit YER
- Credit original amount
- Currency
- Exchange rate
- Credit YER
- Balance YER

### Financial Reports

Reports must show:

- Total collected in YER equivalent
- Total collected by original currency
- SAR cash total
- USD cash total
- YER cash total
- Exchange-rate notes

## Required Tests

### Currency Tests

- Add SAR exchange rate.
- Add USD exchange rate.
- Prevent negative exchange rate.
- Prevent disabled currency use.
- Latest exchange rate returns correctly.

### Payment Tests

1. Payment in YER:
   - AmountOriginal = AmountInYER
   - ExchangeRateToYER = 1
2. Payment in SAR:
   - AmountOriginal preserved
   - CurrencyCode = SAR
   - AmountInYER calculated correctly
3. Payment in USD:
   - AmountOriginal preserved
   - CurrencyCode = USD
   - AmountInYER calculated correctly
4. Receipt displays both currencies.
5. Patient balance calculated in YER.
6. Reports show totals by original currency and YER equivalent.

## Migration Rules

1. Do not break existing payments.
2. Backfill existing payments as:
   - CurrencyCode = YER
   - AmountOriginal = existing Amount
   - ExchangeRateToYER = 1
   - AmountInYER = existing Amount
3. Add nullable fields first if needed.
4. Backfill data.
5. Then add required constraints.
6. Add indexes on:
   - CurrencyCode
   - ExchangeRate RateDate
   - Payment CurrencyCode
   - Payment PaymentDate

## Do Not Do

- Do not remove YER as base currency.
- Do not convert historical payments without preserving original values.
- Do not use live exchange-rate APIs in production without approval.
- Do not make USD/SAR the accounting base currency.
- Do not change every financial table at once without migration plan.

## Acceptance Criteria

This sprint is complete when:

- Language setting exists and Arabic RTL remains stable.
- YER is defined as base currency.
- SAR and USD are enabled currencies.
- Exchange rates can be added and edited by authorized users.
- Payments can be entered in YER, SAR, or USD.
- Payments store both original currency amount and YER equivalent.
- Receipts show original amount, exchange rate, and YER equivalent.
- Patient account statement calculates balance in YER.
- Finance reports show totals in YER and by original currency.
- All changes are covered by EF Core migrations.
