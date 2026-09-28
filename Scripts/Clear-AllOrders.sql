/*
  Limpieza de órdenes de desarrollo.
  Elimina todas las filas de dbo.OrderItems y dbo.Orders.
  No modifica inventario, eventos, tipos de entrada ni usuarios.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @orderItemsBefore int = (SELECT COUNT(*) FROM dbo.OrderItems);
DECLARE @ordersBefore int = (SELECT COUNT(*) FROM dbo.Orders);

BEGIN TRANSACTION;

DELETE FROM dbo.OrderItems;
DELETE FROM dbo.Orders;

COMMIT TRANSACTION;

SELECT
    @orderItemsBefore AS OrderItemsDeleted,
    @ordersBefore AS OrdersDeleted,
    (SELECT COUNT(*) FROM dbo.OrderItems) AS OrderItemsRemaining,
    (SELECT COUNT(*) FROM dbo.Orders) AS OrdersRemaining;
