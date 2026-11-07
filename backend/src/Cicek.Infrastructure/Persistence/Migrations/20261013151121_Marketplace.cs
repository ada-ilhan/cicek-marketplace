using System;
using Cicek.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Marketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUD_AUDIT_ENTRIES",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ACTOR_USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ACTION = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    ENTITY_TYPE = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    ENTITY_ID = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    DETAILS = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    OCCURRED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUD_AUDIT_ENTRIES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CAT_CATEGORIES",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SLUG = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    PARENT_ID = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAT_CATEGORIES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CAT_CATEGORIES_CAT_CATEGORIES_PARENT_ID",
                        column: x => x.PARENT_ID,
                        principalTable: "CAT_CATEGORIES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRT_CARTS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CUSTOMER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    ROW_VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRT_CARTS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CRT_CARTS_IDN_USERS_CUSTOMER_ID",
                        column: x => x.CUSTOMER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ORD_CHECKOUTS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CUSTOMER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    IDEMPOTENCY_KEY = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    CURRENCY_CODE = table.Column<string>(type: "CHAR(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    TOTAL_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORD_CHECKOUTS", x => x.ID);
                    table.CheckConstraint("CK_ORD_CHECKOUTS_CURRENCY", "CURRENCY_CODE IN (''TRY'', ''EUR'', ''USD'', ''GBP'', ''AZN'')");
                    table.ForeignKey(
                        name: "FK_ORD_CHECKOUTS_IDN_USERS_CUSTOMER_ID",
                        column: x => x.CUSTOMER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SHP_SHOPS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    NORMALIZED_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SLUG = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    CONTACT_EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    CONTACT_PHONE = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    ADDRESS_LINE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DISTRICT = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CITY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    POSTAL_CODE = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: false),
                    STATUS = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    STATUS_REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    COMMISSION_RATE_BPS = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    APPROVED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    ROW_VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHP_SHOPS", x => x.ID);
                    table.CheckConstraint("CK_SHP_SHOPS_COMMISSION", "COMMISSION_RATE_BPS IS NULL OR COMMISSION_RATE_BPS BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_SHP_SHOPS_STATUS", "STATUS IN (''PendingApproval'', ''Active'', ''Inactive'', ''Suspended'')");
                });

            migrationBuilder.CreateTable(
                name: "CAT_PRODUCTS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SHOP_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CATEGORY_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    NORMALIZED_NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    PRICE_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    CURRENCY_CODE = table.Column<string>(type: "CHAR(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    STOCK_QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    ROW_VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAT_PRODUCTS", x => x.ID);
                    table.CheckConstraint("CK_CAT_PRODUCTS_CURRENCY", "CURRENCY_CODE IN (''TRY'', ''EUR'', ''USD'', ''GBP'', ''AZN'')");
                    table.CheckConstraint("CK_CAT_PRODUCTS_PRICE", "PRICE_AMOUNT > 0");
                    table.CheckConstraint("CK_CAT_PRODUCTS_STATUS", "STATUS IN (''Active'', ''Inactive'')");
                    table.CheckConstraint("CK_CAT_PRODUCTS_STOCK", "STOCK_QUANTITY >= 0");
                    table.ForeignKey(
                        name: "FK_CAT_PRODUCTS_CAT_CATEGORIES_CATEGORY_ID",
                        column: x => x.CATEGORY_ID,
                        principalTable: "CAT_CATEGORIES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAT_PRODUCTS_SHP_SHOPS_SHOP_ID",
                        column: x => x.SHOP_ID,
                        principalTable: "SHP_SHOPS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORD_ORDERS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ORDER_NUMBER = table.Column<string>(type: "VARCHAR2(15)", unicode: false, maxLength: 15, nullable: false),
                    CHECKOUT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CUSTOMER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SHOP_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SHOP_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    STATUS = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    CURRENCY_CODE = table.Column<string>(type: "CHAR(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    SUBTOTAL_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    DELIVERY_FEE_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    DISCOUNT_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    TOTAL_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    COMMISSION_RATE_BPS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    COMMISSION_BASE_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    COMMISSION_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    SHOP_EARNINGS_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    REFUND_DUE_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    RECIPIENT_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    RECIPIENT_PHONE = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    ADDRESS_LINE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DISTRICT = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CITY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    POSTAL_CODE = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: false),
                    DELIVERY_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    GIFT_MESSAGE = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    PAYMENT_REFERENCE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    CANCELLATION_REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    PAID_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    COMPLETED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    ROW_VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORD_ORDERS", x => x.ID);
                    table.CheckConstraint("CK_ORD_ORDERS_AMOUNTS", "SUBTOTAL_AMOUNT >= 0 AND DELIVERY_FEE_AMOUNT >= 0 AND DISCOUNT_AMOUNT >= 0 AND COMMISSION_AMOUNT >= 0 AND REFUND_DUE_AMOUNT BETWEEN 0 AND TOTAL_AMOUNT");
                    table.CheckConstraint("CK_ORD_ORDERS_COMMISSION_RATE", "COMMISSION_RATE_BPS BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_ORD_ORDERS_COMMISSION_SPLIT", "COMMISSION_AMOUNT + SHOP_EARNINGS_AMOUNT = COMMISSION_BASE_AMOUNT");
                    table.CheckConstraint("CK_ORD_ORDERS_CURRENCY", "CURRENCY_CODE IN (''TRY'', ''EUR'', ''USD'', ''GBP'', ''AZN'')");
                    table.CheckConstraint("CK_ORD_ORDERS_STATUS", "STATUS IN (''PendingPayment'', ''Paid'', ''Accepted'', ''Preparing'', ''ReadyForDelivery'', ''Delivered'', ''Cancelled'', ''Rejected'', ''Expired'')");
                    table.CheckConstraint("CK_ORD_ORDERS_TOTAL", "TOTAL_AMOUNT = SUBTOTAL_AMOUNT + DELIVERY_FEE_AMOUNT - DISCOUNT_AMOUNT");
                    table.ForeignKey(
                        name: "FK_ORD_ORDERS_IDN_USERS_CUSTOMER_ID",
                        column: x => x.CUSTOMER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORD_ORDERS_ORD_CHECKOUTS_CHECKOUT_ID",
                        column: x => x.CHECKOUT_ID,
                        principalTable: "ORD_CHECKOUTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORD_ORDERS_SHP_SHOPS_SHOP_ID",
                        column: x => x.SHOP_ID,
                        principalTable: "SHP_SHOPS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SHP_SHOP_MEMBERS",
                columns: table => new
                {
                    SHOP_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ROLE = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: false),
                    ADDED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHP_SHOP_MEMBERS", x => new { x.SHOP_ID, x.USER_ID });
                    table.CheckConstraint("CK_SHP_SHOP_MEMBERS_ROLE", "ROLE IN (''Owner'', ''Staff'')");
                    table.ForeignKey(
                        name: "FK_SHP_SHOP_MEMBERS_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SHP_SHOP_MEMBERS_SHP_SHOPS_SHOP_ID",
                        column: x => x.SHOP_ID,
                        principalTable: "SHP_SHOPS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CAT_PRODUCT_IMAGES",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PRODUCT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    URL = table.Column<string>(type: "VARCHAR2(1000)", unicode: false, maxLength: 1000, nullable: false),
                    ALT_TEXT = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAT_PRODUCT_IMAGES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CAT_PRODUCT_IMAGES_CAT_PRODUCTS_PRODUCT_ID",
                        column: x => x.PRODUCT_ID,
                        principalTable: "CAT_PRODUCTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CRT_CART_ITEMS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CART_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PRODUCT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ADDED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRT_CART_ITEMS", x => x.ID);
                    table.CheckConstraint("CK_CRT_CART_ITEMS_QUANTITY", "QUANTITY BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_CRT_CART_ITEMS_CAT_PRODUCTS_PRODUCT_ID",
                        column: x => x.PRODUCT_ID,
                        principalTable: "CAT_PRODUCTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CRT_CART_ITEMS_CRT_CARTS_CART_ID",
                        column: x => x.CART_ID,
                        principalTable: "CRT_CARTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ORD_ORDER_ITEMS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ORDER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PRODUCT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PRODUCT_NAME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    UNIT_PRICE_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false),
                    QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LINE_TOTAL_AMOUNT = table.Column<decimal>(type: "DECIMAL(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORD_ORDER_ITEMS", x => x.ID);
                    table.CheckConstraint("CK_ORD_ORDER_ITEMS_LINE_TOTAL", "LINE_TOTAL_AMOUNT = UNIT_PRICE_AMOUNT * QUANTITY");
                    table.CheckConstraint("CK_ORD_ORDER_ITEMS_QUANTITY", "QUANTITY > 0");
                    table.ForeignKey(
                        name: "FK_ORD_ORDER_ITEMS_CAT_PRODUCTS_PRODUCT_ID",
                        column: x => x.PRODUCT_ID,
                        principalTable: "CAT_PRODUCTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORD_ORDER_ITEMS_ORD_ORDERS_ORDER_ID",
                        column: x => x.ORDER_ID,
                        principalTable: "ORD_ORDERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORD_ORDER_STATUS_HISTORY",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ORDER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FROM_STATUS = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: true),
                    TO_STATUS = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    ACTOR = table.Column<string>(type: "VARCHAR2(16)", unicode: false, maxLength: 16, nullable: false),
                    ACTOR_USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    REASON = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    OCCURRED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORD_ORDER_STATUS_HISTORY", x => x.ID);
                    table.CheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_ACTOR", "ACTOR IN (''Customer'', ''Shop'', ''Administrator'', ''System'')");
                    table.CheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_FROM", "FROM_STATUS IS NULL OR FROM_STATUS IN (''PendingPayment'', ''Paid'', ''Accepted'', ''Preparing'', ''ReadyForDelivery'', ''Delivered'', ''Cancelled'', ''Rejected'', ''Expired'')");
                    table.CheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_TO", "TO_STATUS IN (''PendingPayment'', ''Paid'', ''Accepted'', ''Preparing'', ''ReadyForDelivery'', ''Delivered'', ''Cancelled'', ''Rejected'', ''Expired'')");
                    table.ForeignKey(
                        name: "FK_ORD_ORDER_STATUS_HISTORY_ORD_ORDERS_ORDER_ID",
                        column: x => x.ORDER_ID,
                        principalTable: "ORD_ORDERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CAT_CATEGORIES",
                columns: new[] { "ID", "IS_ACTIVE", "NAME", "PARENT_ID", "SLUG", "SORT_ORDER" },
                values: new object[,]
                {
                    { new Guid("0199d6a0-0000-7000-8000-000000000001"), true, "Çiçekler", null, "cicekler", 1 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000002"), true, "Hediyeler", null, "hediyeler", 2 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000011"), true, "Güller", new Guid("0199d6a0-0000-7000-8000-000000000001"), "guller", 1 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000012"), true, "Orkideler", new Guid("0199d6a0-0000-7000-8000-000000000001"), "orkideler", 2 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000013"), true, "Buketler", new Guid("0199d6a0-0000-7000-8000-000000000001"), "buketler", 3 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000014"), true, "Saksı Çiçekleri", new Guid("0199d6a0-0000-7000-8000-000000000001"), "saksi-cicekleri", 4 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000021"), true, "Çikolatalar", new Guid("0199d6a0-0000-7000-8000-000000000002"), "cikolatalar", 1 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000022"), true, "Hediye Sepetleri", new Guid("0199d6a0-0000-7000-8000-000000000002"), "hediye-sepetleri", 2 },
                    { new Guid("0199d6a0-0000-7000-8000-000000000023"), true, "Peluş Oyuncaklar", new Guid("0199d6a0-0000-7000-8000-000000000002"), "pelus-oyuncaklar", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUD_AUDIT_ENTRIES_ENTITY_TYPE_ENTITY_ID",
                table: "AUD_AUDIT_ENTRIES",
                columns: new[] { "ENTITY_TYPE", "ENTITY_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_AUD_AUDIT_ENTRIES_OCCURRED_AT",
                table: "AUD_AUDIT_ENTRIES",
                column: "OCCURRED_AT");

            migrationBuilder.CreateIndex(
                name: "IX_CAT_CATEGORIES_PARENT_ID",
                table: "CAT_CATEGORIES",
                column: "PARENT_ID");

            migrationBuilder.CreateIndex(
                name: "UX_CAT_CATEGORIES_SLUG",
                table: "CAT_CATEGORIES",
                column: "SLUG",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CAT_PRODUCT_IMAGES_PRODUCT_ID",
                table: "CAT_PRODUCT_IMAGES",
                column: "PRODUCT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CAT_PRODUCTS_CATEGORY_ID",
                table: "CAT_PRODUCTS",
                column: "CATEGORY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CAT_PRODUCTS_SHOP_ID_STATUS",
                table: "CAT_PRODUCTS",
                columns: new[] { "SHOP_ID", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_CAT_PRODUCTS_STATUS_CATEGORY_ID",
                table: "CAT_PRODUCTS",
                columns: new[] { "STATUS", "CATEGORY_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_CRT_CART_ITEMS_PRODUCT_ID",
                table: "CRT_CART_ITEMS",
                column: "PRODUCT_ID");

            migrationBuilder.CreateIndex(
                name: "UX_CRT_CART_ITEMS_CART_ID_PRODUCT_ID",
                table: "CRT_CART_ITEMS",
                columns: new[] { "CART_ID", "PRODUCT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CRT_CARTS_CUSTOMER_ID",
                table: "CRT_CARTS",
                column: "CUSTOMER_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ORD_CHECKOUTS_CUSTOMER_ID_IDEMPOTENCY_KEY",
                table: "ORD_CHECKOUTS",
                columns: new[] { "CUSTOMER_ID", "IDEMPOTENCY_KEY" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDER_ITEMS_ORDER_ID",
                table: "ORD_ORDER_ITEMS",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDER_ITEMS_PRODUCT_ID",
                table: "ORD_ORDER_ITEMS",
                column: "PRODUCT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDER_STATUS_HISTORY_ORDER_ID",
                table: "ORD_ORDER_STATUS_HISTORY",
                column: "ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDERS_CHECKOUT_ID",
                table: "ORD_ORDERS",
                column: "CHECKOUT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDERS_CUSTOMER_ID_CREATED_AT",
                table: "ORD_ORDERS",
                columns: new[] { "CUSTOMER_ID", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDERS_SHOP_ID_STATUS_CREATED_AT",
                table: "ORD_ORDERS",
                columns: new[] { "SHOP_ID", "STATUS", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "IX_ORD_ORDERS_STATUS_CREATED_AT",
                table: "ORD_ORDERS",
                columns: new[] { "STATUS", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "UX_ORD_ORDERS_ORDER_NUMBER",
                table: "ORD_ORDERS",
                column: "ORDER_NUMBER",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHP_SHOP_MEMBERS_USER_ID",
                table: "SHP_SHOP_MEMBERS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SHP_SHOPS_STATUS",
                table: "SHP_SHOPS",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "UX_SHP_SHOPS_SLUG",
                table: "SHP_SHOPS",
                column: "SLUG",
                unique: true);

            // PL/SQL v2: audit and order-history tables become append-only for the runtime role (ADR-0010).
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessSpecV2));
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessBodyV2));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous package version.
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessSpecV1));
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessBodyV1));

            migrationBuilder.DropTable(
                name: "AUD_AUDIT_ENTRIES");

            migrationBuilder.DropTable(
                name: "CAT_PRODUCT_IMAGES");

            migrationBuilder.DropTable(
                name: "CRT_CART_ITEMS");

            migrationBuilder.DropTable(
                name: "ORD_ORDER_ITEMS");

            migrationBuilder.DropTable(
                name: "ORD_ORDER_STATUS_HISTORY");

            migrationBuilder.DropTable(
                name: "SHP_SHOP_MEMBERS");

            migrationBuilder.DropTable(
                name: "CRT_CARTS");

            migrationBuilder.DropTable(
                name: "CAT_PRODUCTS");

            migrationBuilder.DropTable(
                name: "ORD_ORDERS");

            migrationBuilder.DropTable(
                name: "CAT_CATEGORIES");

            migrationBuilder.DropTable(
                name: "ORD_CHECKOUTS");

            migrationBuilder.DropTable(
                name: "SHP_SHOPS");
        }
    }
}
