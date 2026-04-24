# Pseudo Code Documentation
**Author:** Ochilov Ilyosjon (ID: B2300540)
**Project:** Urban Style POS System

---

## Algorithm 1: Checking Stock Availability When Adding a Product to the Cart

**Purpose:** To ensure that a cashier cannot add more items to the shopping cart than what is currently available in the inventory.

```text
BEGIN ALGORITHM AddToCart(SelectedProduct)

    // Step 1: Identify the product the user wants to add
    TargetProductID = SelectedProduct.ID
    TargetProductStock = SelectedProduct.StockQuantity

    // Step 2: Check if the product already exists in the current Shopping Cart
    IF ShoppingCart contains an item with ID == TargetProductID THEN
        
        // Step 2a: If it exists, find the current quantity in the cart
        CurrentCartQuantity = ShoppingCart.GetQuantityFor(TargetProductID)
        
        // Step 2b: Verify if adding one more exceeds available stock
        IF (CurrentCartQuantity + 1) <= TargetProductStock THEN
            // Sufficient stock is available
            ShoppingCart.IncreaseQuantityFor(TargetProductID, by 1)
            DisplayMessage("Product quantity updated in cart.")
        ELSE
            // Insufficient stock
            DisplayWarningMessage("Not enough stock available!")
            STOP // Halt the addition process
        END IF

    ELSE
        // Step 3: Product is not yet in the cart
        // Since it's a new addition, check if at least 1 item is in stock
        IF TargetProductStock >= 1 THEN
            ShoppingCart.AddNewItem(SelectedProduct, Quantity = 1)
            DisplayMessage("Product added to cart.")
        ELSE
            DisplayWarningMessage("This product is currently out of stock!")
            STOP
        END IF
        
    END IF

    // Step 4: Update the total price display
    UpdateTotalCartAmountDisplay()

END ALGORITHM
```

---

## Algorithm 2: Updating Stock Quantity When Payment is Completed

**Purpose:** To safely deduct the purchased quantities from the central inventory database only after a successful payment, ensuring data consistency.

```text
BEGIN ALGORITHM CompleteCheckout(ShoppingCart)

    // Step 1: Verify that the cart is not empty
    IF ShoppingCart.IsEmpty() THEN
        DisplayWarningMessage("Shopping cart is empty. Cannot checkout.")
        STOP
    END IF

    // Step 2: Initiate a safe database transaction to prevent partial updates
    BEGIN DATABASE TRANSACTION

    TRY
        // Step 3: Iterate through every item in the shopping cart
        FOR EACH CartItem IN ShoppingCart DO
            
            // Step 3a: Fetch the latest stock amount from the database and lock the row
            CurrentDatabaseStock = Database.GetStockForUpdate(CartItem.ID)
            
            // Step 3b: Double-check if the stock is still sufficient (in case another cashier sold it)
            IF CurrentDatabaseStock < CartItem.Quantity THEN
                THROW ERROR "Insufficient stock for " + CartItem.Name
            END IF
            
            // Step 3c: Calculate the new remaining stock
            NewStockAmount = CurrentDatabaseStock - CartItem.Quantity
            
            // Step 3d: Update the database with the new stock amount
            Database.UpdateStock(CartItem.ID, NewStockAmount)
            
        END FOR

        // Step 4: If all items are processed successfully, save the sale record
        Database.SaveSaleRecord(ShoppingCart.GetTotalAmount(), CurrentDate)

        // Step 5: Commit the transaction permanently
        COMMIT DATABASE TRANSACTION
        
        // Step 6: Post-checkout cleanup
        DisplayMessage("Checkout completed successfully!")
        ShoppingCart.Clear()
        RefreshAvailableProductsList()

    CATCH ERROR AS Exception
        // Step 7: If any error occurred (e.g., insufficient stock or network failure)
        // Undo all changes made during this transaction
        ROLLBACK DATABASE TRANSACTION
        DisplayErrorMessage("Transaction failed: " + Exception.Message)
        
    END TRY

END ALGORITHM
```