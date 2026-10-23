-- ==============================================================================
-- Cadence OrCAD CIS (Component Information System) MariaDB / MySQL 初始化脚本
-- 分类架构：严格遵循 PCBA 01~12 大类及 13 结构件分类标准 (去除 cis_ 前缀)
-- 01: CPU/GPU/MCU/SOC
-- 02: Common IC, other than Power
-- 03: DDR
-- 04: Misc..
-- 05: Flash/ROM
-- 06: Power IC
-- 07: Transistor/Diode
-- 08: PCB
-- 09: Inductor
-- 10: Resistor
-- 11: Capacitor
-- 12: Connector
-- 13: 结构件 (Box-level Mechanical / Structural)
-- ==============================================================================

CREATE DATABASE IF NOT EXISTS `cadence_cis_db` 
    DEFAULT CHARACTER SET utf8mb4 
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE `cadence_cis_db`;

SET FOREIGN_KEY_CHECKS = 0;

-- ------------------------------------------------------------------------------
-- 01. CPU/GPU/MCU/SOC (01_cpu_mcu_soc)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `01_cpu_mcu_soc`;
CREATE TABLE `01_cpu_mcu_soc` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '公司唯一物料编码 (如 010000000001)',
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Microcontroller (MCU)' COMMENT 'CIS目录树分类 (如 MCU, Application Processor, SoC, GPU, FPGA)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称型号或规格 (如 STM32G030F4P6, ESP32-S3)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装代码 (如 TSSOP-20, QFN-48, BGA-256)',
    `Description` VARCHAR(255) DEFAULT NULL COMMENT '物料描述',
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT '' COMMENT 'OrCAD原理图符号 (<库名>\\<符号名>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL DEFAULT '' COMMENT 'Allegro PCB封装名',
    `Model_3D` VARCHAR(128) DEFAULT NULL COMMENT 'STEP 3D模型文件名',
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U' COMMENT '原理图位号前缀',
    `Manufacturer` VARCHAR(128) DEFAULT NULL COMMENT '生产厂商',
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL COMMENT '原厂订货型号 (MPN)',
    `Datasheet` VARCHAR(512) DEFAULT NULL COMMENT '规格书链接',
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active' COMMENT '生命周期状态 (Active / NRND / Obsolete)',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL COMMENT '立创商城物料编号',
    `Price_RMB` VARCHAR(32) DEFAULT NULL COMMENT '参考单价 (元)',
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='01 CPU/GPU/MCU/SOC';

-- ------------------------------------------------------------------------------
-- 02. 通用芯片除电源外 (02_common_ic)
-- 运放、比较器、接口收发器(485/CAN/USB)、逻辑门、电平转换、ADC/DAC
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `02_common_ic`;
CREATE TABLE `02_common_ic` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Operational Amplifier' COMMENT 'CIS分类 (如 OpAmp, RS485 Transceiver, Logic Gate, Level Shifter, ADC)',
    `Value` VARCHAR(64) NOT NULL COMMENT '型号规格 (如 LM358, MAX485, 74LVC1G08)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) DEFAULT NULL,
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='02 Common IC, other than Power';

-- ------------------------------------------------------------------------------
-- 03. DDR / 动态随机存取内存 (03_ddr)
-- LPDDR4X, LPDDR5, DDR4, DDR3
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `03_ddr`;
CREATE TABLE `03_ddr` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'LPDDR4X' COMMENT 'CIS分类 (如 LPDDR4X, LPDDR5, DDR4, DDR3)',
    `Value` VARCHAR(64) NOT NULL COMMENT '容量与规格 (如 4GB LPDDR4X-4266, 8Gb DDR4)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 FBGA-200, BGA-96)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) DEFAULT NULL,
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='03 DDR';

-- ------------------------------------------------------------------------------
-- 04. 杂项器件 (04_misc)
-- 晶体晶振 (Y/OSC)、开关按键 (SW)、保险丝 (F)、蜂鸣器 (BZ)、天线 (ANT)、无线模组
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `04_misc`;
CREATE TABLE `04_misc` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Crystal (Passive)' COMMENT 'CIS分类 (如 Crystal, Oscillator, Tactile Switch, Fuse/PPTC, Buzzer, Antenna)',
    `Value` VARCHAR(64) NOT NULL COMMENT '关键规格 (如 32.768kHz, 24MHz, 0.5A/24V, 85dB)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\XTAL',
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Y' COMMENT '原理图位号前缀 (如 Y, SW, F, BZ)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='04 Misc..';

-- ------------------------------------------------------------------------------
-- 05. 闪存与非易失存储 (05_flash_rom)
-- SPI NOR Flash, NAND Flash, eMMC, UFS, EEPROM
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `05_flash_rom`;
CREATE TABLE `05_flash_rom` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'SPI NOR Flash' COMMENT 'CIS分类 (如 SPI NOR Flash, eMMC, UFS, EEPROM)',
    `Value` VARCHAR(64) NOT NULL COMMENT '容量规格 (如 128M-bit, 32GB, 256K-bit)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOP-8, WSON-8, BGA-153)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) DEFAULT NULL,
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='05 Flash/ROM';

-- ------------------------------------------------------------------------------
-- 06. 电源管理芯片 (06_power_ic)
-- LDO, Buck DCDC, Boost DCDC, PMIC, 锂电池充电管理
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `06_power_ic`;
CREATE TABLE `06_power_ic` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'LDO Regulator' COMMENT 'CIS分类 (如 LDO Regulator, Buck Converter, Boost Converter, PMIC, Charger)',
    `Value` VARCHAR(64) NOT NULL COMMENT '规格概要 (如 3.3V/1A, Adj/3A, 5V/2A)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) DEFAULT NULL,
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='06 Power IC';

-- ------------------------------------------------------------------------------
-- 07. 晶体管与二极管 (07_transistor_diode)
-- MOS管、三极管、肖特基、稳压管、TVS防静电管、LED发光管
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `07_transistor_diode`;
CREATE TABLE `07_transistor_diode` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'N-Channel MOSFET' COMMENT 'CIS分类 (如 N-Channel MOSFET, P-Channel MOSFET, BJT NPN, Schottky Diode, TVS, LED)',
    `Value` VARCHAR(64) NOT NULL COMMENT '核心参数 (如 30V/5.8A, 40V/1A, 3.3V, RED)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\DIODE',
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Q' COMMENT '位号 (Q, D, TVS, LED)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='07 Transistor/Diode';

-- ------------------------------------------------------------------------------
-- 08. PCB光板 (08_pcb)
-- 裸板工程料号
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `08_pcb`;
CREATE TABLE `08_pcb` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Bare PCB' COMMENT 'CIS分类 (如 Bare PCB, Panel PCB)',
    `Value` VARCHAR(64) NOT NULL COMMENT '板名与版本 (如 MAIN_BOARD_REV_A1)',
    `Package` VARCHAR(64) NOT NULL DEFAULT 'PCB' COMMENT '层数与工艺 (如 4-Layer FR4 1.2mm ENIG)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'MECHANICAL\\PCB',
    `PCB_Footprint` VARCHAR(128) NOT NULL DEFAULT 'PCB',
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'PCB',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='08 PCB';

-- ------------------------------------------------------------------------------
-- 09. 电感与磁珠 (09_inductor) 【标准件库：复用 DISCRETE\IND, BEAD】
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `09_inductor`;
CREATE TABLE `09_inductor` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Chip Inductor' COMMENT 'CIS分类 (如 Chip Inductor, Power Inductor, Ferrite Bead)',
    `Value` VARCHAR(64) NOT NULL COMMENT '感值/阻抗 (如 2.2uH, 10uH, 120R@100MHz)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装代码 (如 0402, 0603, 0805, 4040)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\IND',
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'L',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='09 Inductor';

-- ------------------------------------------------------------------------------
-- 10. 电阻 (10_resistor) 【标准件库：复用 DISCRETE\RES】
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `10_resistor`;
CREATE TABLE `10_resistor` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '公司自建唯一物料编码',
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Chip Resistor' COMMENT 'CIS分类 (如 Chip Resistor, Resistor Network)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称值 (如 10k, 4.7R, 0R)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 0402, 0603, 0805, 1206)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\RES',
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'R',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='10 Resistor';

-- ------------------------------------------------------------------------------
-- 11. 电容 (11_capacitor) 【标准件库：复用 DISCRETE\CAP, CAP_POL】
-- 包含 MLCC, 贴片陶瓷, 钽电容/POSCAP, 铝电解
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `11_capacitor`;
CREATE TABLE `11_capacitor` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Ceramic Capacitor (MLCC)' COMMENT 'CIS分类 (如 Ceramic Capacitor (MLCC), Polymer Tantalum (POSCAP), Aluminum Electrolytic)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称容值 (如 100nF, 10uF, 22pF, 47uF/6.3V)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装代码 (如 0201, 0402, 0603, 0805, 3216)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\CAP',
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'C',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='11 Capacitor';

-- ------------------------------------------------------------------------------
-- 12. 连接器与接插件 (12_connector)
-- Type-C, 排针排母, FPC, 板对板, 端子
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `12_connector`;
CREATE TABLE `12_connector` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Pin Header' COMMENT 'CIS分类 (如 USB Type-C, Pin Header, FPC Connector, Terminal Block)',
    `Value` VARCHAR(64) NOT NULL COMMENT '规格简述 (如 16-Pin Type-C, 1x4P 2.54mm)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) DEFAULT NULL,
    `PCB_Footprint` VARCHAR(128) DEFAULT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'J',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='12 Connector';

-- ------------------------------------------------------------------------------
-- 13. 结构件 (13_mechanical)
-- 螺钉、铜柱、屏蔽罩、散热片、外壳、导热垫 (Box-level Hardware)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `13_mechanical`;
CREATE TABLE `13_mechanical` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Shielding Can' COMMENT 'CIS分类 (如 Shielding Can, Heatsink, Screw, Standoff, Enclosure)',
    `Value` VARCHAR(64) NOT NULL COMMENT '规格尺寸 (如 M3x6mm, 20x20x6mm)',
    `Package` VARCHAR(64) NOT NULL DEFAULT 'MECH',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'MECHANICAL\\MECH',
    `PCB_Footprint` VARCHAR(128) NOT NULL DEFAULT 'MECH',
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'MECH',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '分销商/代理商渠道 (若非原厂直供)',
    `Note` VARCHAR(512) DEFAULT NULL COMMENT '自由备注 (供应商联系人/样品批次/特殊说明)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='13 结构件';

-- ------------------------------------------------------------------------------
-- 基础标准数据演示 (Seed Data)
-- ------------------------------------------------------------------------------
INSERT INTO `10_resistor` 
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`) 
VALUES
('100000000001', 'Chip Resistor', '10k',  '0402', 'RES SMD 10K OHM 1% 1/16W 0402',  'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402FR-0710KL', 'Active', 'C25744'),
('100000000002', 'Chip Resistor', '4.7k', '0402', 'RES SMD 4.7K OHM 1% 1/16W 0402', 'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402FR-074K7L', 'Active', 'C25900'),
('100000000003', 'Chip Resistor', '0R',   '0402', 'RES SMD 0 OHM JUMPER 1/16W 0402', 'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402JR-070RL',  'Active', 'C17168'),
('100000000004', 'Chip Resistor', '100R', '0603', 'RES SMD 100 OHM 1% 1/10W 0603',  'DISCRETE\\RES', 'R0603', 'R0603.step', 'R', 'UniOhm',  '0603WAJ0101T5E',  'Active', 'C22775');

INSERT INTO `11_capacitor`
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('110000000001', 'Ceramic Capacitor (MLCC)', '100nF', '0402', 'CAP CER 0.1UF 16V X7R 0402', 'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B104KO5NNNC',    'Active', 'C1525'),
('110000000002', 'Ceramic Capacitor (MLCC)', '10nF',  '0402', 'CAP CER 10NF 50V X7R 0402',   'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B103KB5NNNC',    'Active', 'C1523'),
('110000000003', 'Ceramic Capacitor (MLCC)', '22pF',  '0402', 'CAP CER 22PF 50V C0G 0402',   'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Murata',  'GRM1555C1H220JA01D', 'Active', 'C1554'),
('110000000004', 'Ceramic Capacitor (MLCC)', '10uF',  '0603', 'CAP CER 10UF 10V X5R 0603',   'DISCRETE\\CAP', 'C0603', 'C0603.step', 'C', 'Samsung', 'CL10A106MP8NNNC',    'Active', 'C15850'),
('110000000005', 'Polymer Tantalum (POSCAP)','47uF/6.3V','3528','CAP TANT POLY 47UF 6.3V 3528','DISCRETE\\CAP_POL','CAP_TANT_3528_B','CAP_TANT_3528_B.step','C','Panasonic','6TPB47M','Active','C10850');

INSERT INTO `09_inductor`
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('090000000001', 'Chip Inductor', '2.2uH', '0603', 'IND 2.2UH 0603 SMD', 'DISCRETE\\IND', 'L0603', 'L0603.step', 'L', 'Sunlord', 'SDCL1608C2R2KTDF', 'Active', 'C1045');

SET FOREIGN_KEY_CHECKS = 1;

-- ------------------------------------------------------------------------------
-- 全局汇总检索视图 (聚合 01~13 表)
-- ------------------------------------------------------------------------------
CREATE OR REPLACE VIEW `v_all_components` AS
SELECT '01 CPU/GPU/MCU/SOC' AS `Category`, `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `01_cpu_mcu_soc`
UNION ALL
SELECT '02 Common IC', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `02_common_ic`
UNION ALL
SELECT '03 DDR', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `03_ddr`
UNION ALL
SELECT '04 Misc..', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `04_misc`
UNION ALL
SELECT '05 Flash/ROM', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `05_flash_rom`
UNION ALL
SELECT '06 Power IC', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `06_power_ic`
UNION ALL
SELECT '07 Transistor/Diode', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `07_transistor_diode`
UNION ALL
SELECT '08 PCB', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `08_pcb`
UNION ALL
SELECT '09 Inductor', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `09_inductor`
UNION ALL
SELECT '10 Resistor', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `10_resistor`
UNION ALL
SELECT '11 Capacitor', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `11_capacitor`
UNION ALL
SELECT '12 Connector', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `12_connector`
UNION ALL
SELECT '13 结构件', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` FROM `13_mechanical`;


-- ==============================================================================
-- 研发样品库存管控系统 (Sample Inventory Management)
-- ==============================================================================

-- 1. 样品台账主表 (按物料号与库位汇总)
DROP TABLE IF EXISTS `sample_inventory`;
CREATE TABLE `sample_inventory` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL COMMENT '关联 OrCAD CIS 物料编号',
    `Location` VARCHAR(64) NOT NULL DEFAULT '默认样品柜' COMMENT '物理存放库位 (如 A柜-02层-15盒)',
    `Quantity_On_Hand` INT NOT NULL DEFAULT 0 COMMENT '当前在手可用库存',
    `Quantity_Lent` INT NOT NULL DEFAULT 0 COMMENT '调测在借数量 (借出待归还)',
    `Quantity_Total_In` INT NOT NULL DEFAULT 0 COMMENT '累计入库总数',
    `Min_Alert_Qty` INT NOT NULL DEFAULT 0 COMMENT '低库存预警阈值 (0表示不开启预警)',
    `Distributor` VARCHAR(128) DEFAULT NULL COMMENT '主要供应/分销渠道',
    `Remark` VARCHAR(512) DEFAULT NULL COMMENT '库位或物料备注',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_pn_loc` (`Part_Number`, `Location`),
    INDEX `idx_part_number` (`Part_Number`),
    INDEX `idx_location` (`Location`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='研发样品库存主表';

-- 2. 样品出入库变动明细与流水台账 (Transactions Log)
DROP TABLE IF EXISTS `sample_transactions`;
CREATE TABLE `sample_transactions` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL COMMENT '物料编号',
    `Transaction_Type` VARCHAR(32) NOT NULL COMMENT '业务类型: 入库 / 领用消耗 / 调测借出 / 归还 / 报废',
    `Quantity` INT NOT NULL COMMENT '变动数量 (正整数)',
    `Operator` VARCHAR(64) NOT NULL COMMENT '领用人/经办人',
    `Project_Name` VARCHAR(128) DEFAULT NULL COMMENT '关联研发项目/板卡代号',
    `Location` VARCHAR(64) NOT NULL DEFAULT '默认样品柜' COMMENT '库位',
    `Remark` VARCHAR(512) DEFAULT NULL COMMENT '详细原因或说明',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_tx_pn` (`Part_Number`),
    INDEX `idx_tx_type` (`Transaction_Type`),
    INDEX `idx_tx_operator` (`Operator`),
    INDEX `idx_tx_created` (`Created_At`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='研发样品出入库变动流水表';
