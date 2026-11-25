# Demo guide

Run the local setup, migrations, seed and start scripts from the [README](../README.md). Sign in as the seeded administrator and use the local password in .work/dev.env.json. The seed records are fictitious. Inventory opening balances are posted by the same stock service used by the API.

## Review a transfer

1. Open Tồn kho and search VP-01-02. Confirm the positions and available quantities.
2. Under Chứng từ kho, choose Lập phiếu and select Chuyển kho.
3. Choose Kho Hà Nội and Kho Hồ Chí Minh, add VP-01-02, enter 3 and save the draft.
4. Ghi sổ the draft. Open Biến động kho and find two rows from the same document, with -3 and +3.
5. Return to the transfer and choose Đảo chứng từ before creating another movement on the affected positions. Check that the two warehouse quantities return to their previous values.

The purchasing-to-payables and sales-to-receivables demos depend on the order and finance milestones. See [project status](PROJECT_STATUS.md).
