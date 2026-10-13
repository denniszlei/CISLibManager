-- ==============================================================================
-- Cadence OrCAD CIS (Component Information System) MariaDB / MySQL 初始化脚本
-- 架构优化版：精简统一规范，仅保留核心通用字段与 Value、Package，增加 Part_Type
-- 兼容：Cadence 17.4 / 22.1 / 23.1 + MariaDB 10.5+ / MySQL 8.0+
-- 字符集：utf8mb4 (支持各种特殊技术符号，如 Ω, µ, ±, ℃, ⌀)
-- ==============================================================================

CREATE DATABASE IF NOT EXISTS `cadence_cis_db` 
    DEFAULT CHARACTER SET utf8mb4 
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE `cadence_cis_db`;

SET FOREIGN_KEY_CHECKS = 0;

-- ==============================================================================
-- 统一标准表结构说明：
-- 1. Part_Type: Cadence OrCAD CIS 官方推荐的关键属性，用于在 CIS Explorer 中自动生成层级目录树
-- 2. Value: 核心电气标称值 (电阻为阻值如 10k，电容为容值如 100nF，IC为型号后缀/规格)
-- 3. Package: 封装规格 (如 0402, 0603, SOT-23, LQFP-48)
-- 4. Schematic_Part: 原理图符号 (<库名>\<符号名>，如 DISCRETE\RES)
-- 5. PCB_Footprint: Allegro PCB 封装符号名 (如 R0402, C0603)
-- 6. Model_3D: 3D STEP 模型图档 (如 R0402.step)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. 电阻表 (cis_resistors)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_resistors`;
CREATE TABLE `cis_resistors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '公司自建唯一物料号',
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Chip Resistor' COMMENT 'Cadence CIS 目录树分类 (如 Chip Resistor, Resistor Network)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称值 (如 10k, 4.7R, 0R)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 0402, 0603, 0805, 1206)',
    `Description` VARCHAR(255) DEFAULT NULL COMMENT '物料描述',
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\RES' COMMENT 'OrCAD原理图符号 (<库名>\\<符号名>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL COMMENT 'Allegro PCB封装名 (如 R0402, R0603)',
    `Model_3D` VARCHAR(128) DEFAULT NULL COMMENT 'STEP 3D模型文件名 (如 R0402.step)',
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'R' COMMENT '原理图位号前缀',
    `Manufacturer` VARCHAR(128) DEFAULT NULL COMMENT '生产厂商',
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL COMMENT '原厂订货型号 (MPN)',
    `Datasheet` VARCHAR(512) DEFAULT NULL COMMENT '规格书链接',
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active' COMMENT '生命周期状态 (Active / NRND / Obsolete)',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL COMMENT '立创商城物料编号 (如 C25744)',
    `Price_RMB` VARCHAR(32) DEFAULT NULL COMMENT '参考单价 (元)',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '录入时间',
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电阻物料表';

-- ------------------------------------------------------------------------------
-- 2. 电容表 (cis_capacitors)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_capacitors`;
CREATE TABLE `cis_capacitors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Ceramic Capacitor' COMMENT 'CIS 目录树分类 (如 Ceramic Capacitor, Electrolytic Capacitor, Tantalum Capacitor)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称容值 (如 100nF, 10uF, 22pF)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 0402, 0603, CASE-A/3216)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\CAP',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'C',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电容物料表';

-- ------------------------------------------------------------------------------
-- 3. 电感与磁珠表 (cis_inductors)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_inductors`;
CREATE TABLE `cis_inductors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Chip Inductor' COMMENT 'CIS 目录树分类 (如 Chip Inductor, Power Inductor, Ferrite Bead)',
    `Value` VARCHAR(64) NOT NULL COMMENT '感值/阻抗 (如 2.2uH, 10uH, 120R@100MHz)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\IND',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'L',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电感与磁珠物料表';

-- ------------------------------------------------------------------------------
-- 4. 二极管与整流器件表 (cis_diodes)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_diodes`;
CREATE TABLE `cis_diodes` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Schottky Diode' COMMENT 'CIS 目录树分类 (如 Schottky Diode, Zener Diode, TVS Diode, LED, Rectifier)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '关键规格 (如 40V/1A, 3.3V, RED)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'D',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='二极管与保护器件表';

-- ------------------------------------------------------------------------------
-- 5. 晶体管与MOSFET表 (cis_transistors)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_transistors`;
CREATE TABLE `cis_transistors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'N-Channel MOSFET' COMMENT 'CIS 目录树分类 (如 N-Channel MOSFET, P-Channel MOSFET, NPN BJT, PNP BJT)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '关键规格 (如 30V/5.8A, NPN/40V)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Q',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='晶体管与MOSFET表';

-- ------------------------------------------------------------------------------
-- 6. 电源管理芯片表 (cis_power_ics)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_power_ics`;
CREATE TABLE `cis_power_ics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'LDO Regulator' COMMENT 'CIS 目录树分类 (如 LDO Regulator, Buck Converter, Boost Converter, Battery Charger)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '型号规格 (如 3.3V/1A, Adj/3A)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电源管理芯片表';

-- ------------------------------------------------------------------------------
-- 7. 微处理器与主控表 (cis_mcus)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_mcus`;
CREATE TABLE `cis_mcus` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT '32-Bit MCU' COMMENT 'CIS 目录树分类 (如 32-Bit MCU, 8-Bit MCU, Wireless SoC, FPGA / CPLD, DSP)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '核心规格 (如 Cortex-M0+/64K, ESP32-WROOM)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='微处理器芯片表';

-- ------------------------------------------------------------------------------
-- 8. 模拟与接口芯片表 (cis_analog_ics)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_analog_ics`;
CREATE TABLE `cis_analog_ics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Operational Amplifier' COMMENT 'CIS 目录树分类 (如 Operational Amplifier, RS-485 Transceiver, CAN Transceiver, ADC / DAC)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '规格概要 (如 Dual/10MHz, 3.3V/500kbps)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='模拟与接口芯片表';

-- ------------------------------------------------------------------------------
-- 9. 逻辑器件与存储器表 (cis_logic_memory)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_logic_memory`;
CREATE TABLE `cis_logic_memory` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'SPI NOR Flash' COMMENT 'CIS 目录树分类 (如 SPI NOR Flash, I2C EEPROM, Logic Gate, Level Shifter)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '容量或功能 (如 128M-bit, 2K-bit, Octal Buffer)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='数字逻辑与存储器表';

-- ------------------------------------------------------------------------------
-- 10. 连接器与接插件表 (cis_connectors)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_connectors`;
CREATE TABLE `cis_connectors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Pin Header' COMMENT 'CIS 目录树分类 (如 USB Connector, Pin Header, Female Header, Terminal Block, FPC Connector)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '规格简述 (如 16-Pin/Type-C, 1x4P/2.54mm)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'J',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='连接器与端子表';

-- ------------------------------------------------------------------------------
-- 11. 晶体晶振与时钟表 (cis_crystals)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_crystals`;
CREATE TABLE `cis_crystals` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Crystal (Passive)' COMMENT 'CIS 目录树分类 (如 Crystal (Passive), Oscillator (Active), TCXO)',
    `Value` VARCHAR(64) NOT NULL COMMENT '频率规格 (如 32.768kHz, 24MHz, 25MHz)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Y',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='晶体与晶振时钟表';

-- ------------------------------------------------------------------------------
-- 12. 机电与杂项器件表 (cis_miscellaneous)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_miscellaneous`;
CREATE TABLE `cis_miscellaneous` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE,
    `Part_Type` VARCHAR(64) NOT NULL DEFAULT 'Tactile Switch' COMMENT 'CIS 目录树分类 (如 Tactile Switch, PPTC Resettable Fuse, Buzzer, Relay, Antenna, Module)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '核心规格 (如 0.5A/24V, 85dB, 2.4GHz)',
    `Package` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'SW',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_type` (`Part_Type`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='机电与杂项器件表';

-- ------------------------------------------------------------------------------
-- 示例基础数据 (Seed Data)
-- ------------------------------------------------------------------------------
INSERT INTO `cis_resistors` 
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`) 
VALUES
('RES-0402-10K-00001', 'Chip Resistor', '10k',  '0402', 'RES SMD 10K OHM 1% 1/16W 0402',      'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402FR-0710KL', 'Active', 'C25744'),
('RES-0402-4K7-00002', 'Chip Resistor', '4.7k', '0402', 'RES SMD 4.7K OHM 1% 1/16W 0402',     'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402FR-074K7L', 'Active', 'C25900'),
('RES-0402-0R-00003',  'Chip Resistor', '0R',   '0402', 'RES SMD 0 OHM JUMPER 1/16W 0402',     'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo',   'RC0402JR-070RL',  'Active', 'C17168'),
('RES-0603-100R-00004','Chip Resistor', '100R', '0603', 'RES SMD 100 OHM 1% 1/10W 0603',      'DISCRETE\\RES', 'R0603', 'R0603.step', 'R', 'UniOhm',  '0603WAJ0101T5E',  'Active', 'C22775');

INSERT INTO `cis_capacitors`
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('CAP-0402-100NF-00001', 'Ceramic Capacitor', '100nF', '0402', 'CAP CER 0.1UF 16V X7R 0402',  'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B104KO5NNNC',    'Active', 'C1525'),
('CAP-0402-10NF-00002',  'Ceramic Capacitor', '10nF',  '0402', 'CAP CER 10NF 50V X7R 0402',    'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B103KB5NNNC',    'Active', 'C1523'),
('CAP-0402-22PF-00003',  'Ceramic Capacitor', '22pF',  '0402', 'CAP CER 22PF 50V C0G 0402',    'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Murata',  'GRM1555C1H220JA01D', 'Active', 'C1554'),
('CAP-0603-10UF-00004',  'Ceramic Capacitor', '10uF',  '0603', 'CAP CER 10UF 10V X5R 0603',    'DISCRETE\\CAP', 'C0603', 'C0603.step', 'C', 'Samsung', 'CL10A106MP8NNNC',    'Active', 'C15850');

INSERT INTO `cis_power_ics`
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('PMIC-SOT223-AMS1117-00001', 'LDO Regulator', '3.3V/1A', 'SOT-223', 'IC REG LIN 3.3V 1A SOT-223', 'POWER\\AMS1117-3.3', 'SOT-223', 'SOT-223.step', 'U', 'Advanced Monolithic', 'AMS1117-3.3', 'Active', 'C6186');

INSERT INTO `cis_mcus`
(`Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('MCU-TSSOP20-STM32G030-00001', '32-Bit MCU', 'Cortex-M0+/64K', 'TSSOP-20', 'IC MCU 32BIT 64KB FLASH TSSOP-20', 'MCU\\STM32G030F4P6', 'TSSOP-20', 'TSSOP-20.step', 'U', 'STMicroelectronics', 'STM32G030F4P6', 'Active', 'C529329');

SET FOREIGN_KEY_CHECKS = 1;

-- ------------------------------------------------------------------------------
-- 全局汇总检索视图 (结构高度统一，查询极快)
-- ------------------------------------------------------------------------------
CREATE OR REPLACE VIEW `v_all_components` AS
SELECT 'Resistors' AS `Category`, `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_resistors`
UNION ALL
SELECT 'Capacitors', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_capacitors`
UNION ALL
SELECT 'Inductors', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_inductors`
UNION ALL
SELECT 'Diodes', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_diodes`
UNION ALL
SELECT 'Transistors', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_transistors`
UNION ALL
SELECT 'Power_ICs', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_power_ics`
UNION ALL
SELECT 'MCUs', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_mcus`
UNION ALL
SELECT 'Analog_ICs', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_analog_ics`
UNION ALL
SELECT 'Logic_Memory', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_logic_memory`
UNION ALL
SELECT 'Connectors', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_connectors`
UNION ALL
SELECT 'Crystals', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_crystals`
UNION ALL
SELECT 'Miscellaneous', `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_miscellaneous`;
