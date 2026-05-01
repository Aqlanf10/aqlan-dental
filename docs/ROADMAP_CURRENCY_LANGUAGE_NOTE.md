# Roadmap Note — Language and Multi-Currency Support

A dedicated sprint has been added for language settings and multi-currency financial handling:

- `docs/SPRINT_17B_LOCALIZATION_CURRENCY.md`

## Purpose

This sprint adds:

1. Language change from Settings.
2. Arabic RTL as the default language.
3. Preparation for English later.
4. Yemeni Rial (YER) as the base accounting currency.
5. Saudi Riyal (SAR) and US Dollar (USD) as accepted payment currencies.
6. Exchange-rate management against Yemeni Rial.
7. Patient accounts and receipts that preserve original currency and calculate YER equivalent.

## Implementation Timing

This sprint should be implemented after finance basics are stable because it affects:

- Payments
- Receipts
- Invoices
- Account statements
- Financial reports
- Patient balances

## Main Accounting Rule

YER remains the base ledger currency.

SAR and USD payments must store:

- Original amount
- Original currency
- Exchange rate to YER
- YER equivalent
- Exchange-rate date/source

Do not convert historical payments destructively.
